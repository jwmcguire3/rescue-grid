using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rescue.Content
{
    public static class LevelPacketDesignReportBuilder
    {
        private const int WaterIntervalSharpDropThreshold = 3;
        private const double DensitySharpJumpThreshold = 0.10d;
        private const int PressureRoleRunThreshold = 3;
        private const int PrimarySkillRunThreshold = 3;
        private const double AssistanceIncreaseEpsilon = 0.001d;
        private const string OnboardingCampaignBand = "production_onboarding";

        public static LevelPacketDesignReport Build(string manifestPath, string levelsDir, string briefsDir)
        {
            List<ValidationError> reportFindings = new List<ValidationError>();
            LevelPacketManifest? manifest = ReadManifest(manifestPath, reportFindings);
            if (manifest is null)
            {
                return BuildFatalReport(manifestPath, levelsDir, briefsDir, reportFindings);
            }

            Dictionary<string, string> levelPathsById = GetLevelPathsById(levelsDir, reportFindings);
            Dictionary<string, string> briefPathsById = GetBriefPathsById(briefsDir, reportFindings);
            List<PacketLevelDesignInfo> levels = ReadPacketLevels(manifest, levelPathsById, briefPathsById, reportFindings);

            AddSequenceWarnings(manifest, levels, reportFindings);

            StringBuilder builder = new StringBuilder();
            AppendHeader(builder, manifest);
            AppendIdentity(builder, manifestPath, levelsDir, briefsDir);
            AppendPresence(builder, manifest, levelPathsById, briefPathsById);
            AppendProgression(builder, levels);
            AppendMechanicIntroductions(builder, levels);
            AppendBriefSequences(builder, levels);
            AppendWarningsByLevel(builder, manifest, reportFindings);

            return new LevelPacketDesignReport(
                manifest.PacketId,
                builder.ToString(),
                HasErrors(reportFindings),
                reportFindings);
        }

        private static LevelPacketManifest? ReadManifest(string manifestPath, List<ValidationError> findings)
        {
            if (!File.Exists(manifestPath))
            {
                findings.Add(Error(
                    "packet.manifest.missing",
                    $"Packet manifest was not found at '{manifestPath}'.",
                    "$"));
                return null;
            }

            try
            {
                return LevelPacketManifestLoader.Load(manifestPath);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ContentJsonException)
            {
                findings.Add(Error(
                    "packet.manifest.invalid",
                    ex.Message,
                    "$"));
                return null;
            }
        }

        private static LevelPacketDesignReport BuildFatalReport(
            string manifestPath,
            string levelsDir,
            string briefsDir,
            IReadOnlyList<ValidationError> findings)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Packet Design Report: <unavailable>");
            builder.AppendLine("=====================================");
            AppendIdentity(builder, manifestPath, levelsDir, briefsDir);
            AppendFindingList(builder, "Warnings by Level:", findings);
            return new LevelPacketDesignReport("<unavailable>", builder.ToString(), HasErrors(findings), findings);
        }

        private static List<PacketLevelDesignInfo> ReadPacketLevels(
            LevelPacketManifest manifest,
            Dictionary<string, string> levelPathsById,
            Dictionary<string, string> briefPathsById,
            List<ValidationError> findings)
        {
            List<PacketLevelDesignInfo> levels = new List<PacketLevelDesignInfo>();
            string[] expectedIds = manifest.ExpectedLevelIds ?? Array.Empty<string>();
            for (int i = 0; i < expectedIds.Length; i++)
            {
                string id = expectedIds[i];
                LevelJson? level = ReadLevel(id, levelPathsById, findings);
                LevelBrief? brief = ReadBrief(id, briefPathsById, findings);
                LevelReadabilityMetrics? metrics = null;
                IReadOnlyCollection<string> detectedMechanics = Array.Empty<string>();

                if (level is not null)
                {
                    try
                    {
                        metrics = LevelReadabilityAnalyzer.Analyze(level);
                        detectedMechanics = BriefConformanceValidator.DetectMechanics(level);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                    {
                findings.Add(Error(
                    "packet.level.metricsUnavailable",
                    $"Could not analyze level '{id}': {ex.Message}",
                    LevelPath(id, "board.tiles")));
                    }
                }

                levels.Add(new PacketLevelDesignInfo(
                    Index: i,
                    Id: id,
                    Level: level,
                    Brief: brief,
                    Metrics: metrics,
                    DetectedMechanics: detectedMechanics));
            }

            return levels;
        }

        private static LevelJson? ReadLevel(string id, Dictionary<string, string> levelPathsById, List<ValidationError> findings)
        {
            if (!levelPathsById.TryGetValue(id, out string? levelPath))
            {
                findings.Add(Error(
                    "packet.level.missing",
                    $"Level JSON was not found for packet level '{id}'.",
                    LevelPath(id)));
                return null;
            }

            string json = File.ReadAllText(levelPath);
            ValidationResult result = Validator.Validate(json);
            if (result.HasErrors)
            {
                AddScopedFindings(findings, id, result);
                return null;
            }

            try
            {
                return ContentJson.DeserializeLevel(json);
            }
            catch (ContentJsonException ex)
            {
                string path = string.IsNullOrWhiteSpace(ex.Path) ? "$" : ex.Path;
                findings.Add(Error(
                    "packet.level.invalid",
                    $"Level '{id}' could not be deserialized: {ex.Message}",
                    LevelPath(id, path)));
                return null;
            }
        }

        private static LevelBrief? ReadBrief(string id, Dictionary<string, string> briefPathsById, List<ValidationError> findings)
        {
            if (!briefPathsById.TryGetValue(id, out string? briefPath))
            {
                findings.Add(Error(
                    "packet.brief.missing",
                    $"Level brief was not found for packet level '{id}'.",
                    LevelPath(id)));
                return null;
            }

            string json = File.ReadAllText(briefPath);
            ValidationResult result = LevelBriefLoader.ValidateJson(json);
            if (result.HasErrors)
            {
                AddScopedFindings(findings, id, result);
                return null;
            }

            try
            {
                return ContentJson.DeserializeLevelBrief(json);
            }
            catch (ContentJsonException ex)
            {
                string path = string.IsNullOrWhiteSpace(ex.Path) ? "$" : ex.Path;
                findings.Add(Error(
                    "packet.brief.invalid",
                    $"Brief '{id}' could not be deserialized: {ex.Message}",
                    LevelPath(id, path)));
                return null;
            }
        }

        private static void AddSequenceWarnings(
            LevelPacketManifest manifest,
            IReadOnlyList<PacketLevelDesignInfo> levels,
            List<ValidationError> findings)
        {
            AddMechanicIntroductionWarnings(levels, findings);
            AddDockJamWarnings(manifest, levels, findings);
            AddDebrisPoolWarnings(levels, findings);
            AddWaterIntervalWarnings(levels, findings);
            AddAssistanceWarnings(levels, findings);
            AddPressureRunWarnings(levels, findings);
            AddPrimarySkillRunWarnings(levels, findings);
            AddDensityJumpWarnings(levels, findings);
        }

        private static void AddMechanicIntroductionWarnings(IReadOnlyList<PacketLevelDesignInfo> levels, List<ValidationError> findings)
        {
            Dictionary<string, int> introductions = BuildMechanicIntroductionIndices(levels);
            for (int i = 0; i < levels.Count; i++)
            {
                PacketLevelDesignInfo info = levels[i];
                foreach (string mechanic in info.DetectedMechanics)
                {
                    if (!introductions.TryGetValue(mechanic, out int introductionIndex) || i < introductionIndex)
                    {
                        string introduction = introductionIndex >= 0 && introductionIndex < levels.Count
                            ? levels[introductionIndex].Id
                            : "no brief introduction";
                        findings.Add(Warning(
                            "packet.mechanic.beforeBriefIntroduction",
                            $"Level '{info.Id}' uses mechanic '{mechanic}' before its brief introduction ({introduction}).",
                            LevelPath(info.Id, "allowedMechanics")));
                    }
                }
            }
        }

        private static Dictionary<string, int> BuildMechanicIntroductionIndices(IReadOnlyList<PacketLevelDesignInfo> levels)
        {
            Dictionary<string, int> introductions = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < levels.Count; i++)
            {
                string[] allowed = levels[i].Brief?.AllowedMechanics ?? Array.Empty<string>();
                for (int m = 0; m < allowed.Length; m++)
                {
                    string mechanic = allowed[m];
                    if (!string.IsNullOrWhiteSpace(mechanic) && !introductions.ContainsKey(mechanic))
                    {
                        introductions[mechanic] = i;
                    }
                }
            }

            return introductions;
        }

        private static void AddDockJamWarnings(
            LevelPacketManifest manifest,
            IReadOnlyList<PacketLevelDesignInfo> levels,
            List<ValidationError> findings)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                LevelJson? level = levels[i].Level;
                if (level is not null
                    && level.Dock.JamEnabled
                    && !Contains(manifest.DockJamLevelIds, levels[i].Id))
                {
                    findings.Add(Warning(
                        "packet.dockJam.outsideConfiguredLevels",
                        $"Level '{levels[i].Id}' enables Dock Jam outside configured packet levels ({FormatIds(manifest.DockJamLevelIds)}).",
                        LevelPath(levels[i].Id, "dock.jamEnabled")));
                }
            }
        }

        private static void AddDebrisPoolWarnings(IReadOnlyList<PacketLevelDesignInfo> levels, List<ValidationError> findings)
        {
            PacketLevelDesignInfo? previous = null;
            for (int i = 0; i < levels.Count; i++)
            {
                PacketLevelDesignInfo current = levels[i];
                if (current.Level is null)
                {
                    continue;
                }

                if (previous?.Level is not null
                    && current.Level.DebrisTypePool.Length < previous.Level.DebrisTypePool.Length)
                {
                    findings.Add(Warning(
                        "packet.debrisPool.decrease",
                        $"Debris pool size decreases from {previous.Id} ({previous.Level.DebrisTypePool.Length}) to {current.Id} ({current.Level.DebrisTypePool.Length}).",
                        LevelPath(current.Id, "debrisTypePool")));
                }

                previous = current;
            }
        }

        private static void AddWaterIntervalWarnings(IReadOnlyList<PacketLevelDesignInfo> levels, List<ValidationError> findings)
        {
            PacketLevelDesignInfo? previous = null;
            for (int i = 0; i < levels.Count; i++)
            {
                PacketLevelDesignInfo current = levels[i];
                int currentInterval = current.Level?.Water.RiseInterval ?? 0;
                if (current.Level is null || currentInterval <= 0)
                {
                    continue;
                }

                int previousInterval = previous?.Level?.Water.RiseInterval ?? 0;
                if (previous?.Level is not null
                    && previousInterval > 0
                    && previousInterval - currentInterval >= WaterIntervalSharpDropThreshold)
                {
                    findings.Add(Warning(
                        "packet.waterInterval.sharpDrop",
                        $"Water interval drops from {previous.Id} ({previousInterval}) to {current.Id} ({currentInterval}).",
                        LevelPath(current.Id, "water.riseInterval")));
                }

                previous = current;
            }
        }

        private static void AddAssistanceWarnings(IReadOnlyList<PacketLevelDesignInfo> levels, List<ValidationError> findings)
        {
            int firstPostOnboardingIndex = FindFirstPostOnboardingIndex(levels);
            if (firstPostOnboardingIndex < 0)
            {
                return;
            }

            for (int i = Math.Max(1, firstPostOnboardingIndex); i < levels.Count; i++)
            {
                LevelJson? previous = levels[i - 1].Level;
                LevelJson? current = levels[i].Level;
                if (previous is null || current is null)
                {
                    continue;
                }

                if (current.Assistance.Chance - previous.Assistance.Chance > AssistanceIncreaseEpsilon
                    && !HasAssistanceIncreaseJustification(current, levels[i].Brief))
                {
                    findings.Add(Warning(
                        "packet.assistance.increaseAfterOnboarding",
                        $"Assistance chance increases after onboarding from {levels[i - 1].Id} ({FormatDouble(previous.Assistance.Chance)}) to {levels[i].Id} ({FormatDouble(current.Assistance.Chance)}) without notes.",
                        LevelPath(levels[i].Id, "assistance.chance")));
                }
            }
        }

        private static int FindFirstPostOnboardingIndex(IReadOnlyList<PacketLevelDesignInfo> levels)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                string? campaignBand = levels[i].Brief?.CampaignBand;
                if (!string.IsNullOrWhiteSpace(campaignBand)
                    && !string.Equals(campaignBand, OnboardingCampaignBand, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void AddPressureRunWarnings(IReadOnlyList<PacketLevelDesignInfo> levels, List<ValidationError> findings)
        {
            AddRunWarning(
                levels,
                valueSelector: info => info.Brief?.Role,
                match: "pressure",
                threshold: PressureRoleRunThreshold,
                code: "packet.role.pressureRun",
                messagePrefix: "Too many pressure-role levels in a row",
                path: "$.role",
                findings);
        }

        private static void AddPrimarySkillRunWarnings(IReadOnlyList<PacketLevelDesignInfo> levels, List<ValidationError> findings)
        {
            int runStart = 0;
            string? previous = null;
            for (int i = 0; i <= levels.Count; i++)
            {
                string? current = i < levels.Count ? Normalize(levels[i].Brief?.PrimarySkill) : null;
                if (i > 0 && (!string.Equals(current, previous, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(current)))
                {
                    int runLength = i - runStart;
                    if (!string.IsNullOrWhiteSpace(previous) && runLength >= PrimarySkillRunThreshold)
                    {
                        findings.Add(Warning(
                            "packet.primarySkill.repeatedRun",
                            $"Primary skill '{previous}' repeats for {runLength} levels from {levels[runStart].Id} to {levels[i - 1].Id}.",
                            "$.primarySkill"));
                    }

                    runStart = i;
                }

                previous = current;
            }
        }

        private static void AddDensityJumpWarnings(IReadOnlyList<PacketLevelDesignInfo> levels, List<ValidationError> findings)
        {
            PacketLevelDesignInfo? previous = null;
            for (int i = 0; i < levels.Count; i++)
            {
                PacketLevelDesignInfo current = levels[i];
                if (current.Metrics is null)
                {
                    continue;
                }

                if (previous?.Metrics is not null)
                {
                    double delta = Math.Abs(current.Metrics.VisualOccupancyRatio - previous.Metrics.VisualOccupancyRatio);
                    if (delta >= DensitySharpJumpThreshold)
                    {
                        findings.Add(Warning(
                            "packet.density.sharpJump",
                            $"Density changes sharply from {previous.Id} ({FormatPercent(previous.Metrics.VisualOccupancyRatio)}) to {current.Id} ({FormatPercent(current.Metrics.VisualOccupancyRatio)}).",
                            LevelPath(current.Id, "board.tiles")));
                    }
                }

                previous = current;
            }
        }

        private static void AddRunWarning(
            IReadOnlyList<PacketLevelDesignInfo> levels,
            Func<PacketLevelDesignInfo, string?> valueSelector,
            string match,
            int threshold,
            string code,
            string messagePrefix,
            string path,
            List<ValidationError> findings)
        {
            int runStart = -1;
            for (int i = 0; i <= levels.Count; i++)
            {
                bool isMatch = i < levels.Count
                    && string.Equals(valueSelector(levels[i]), match, StringComparison.Ordinal);
                if (isMatch && runStart < 0)
                {
                    runStart = i;
                }

                if (!isMatch && runStart >= 0)
                {
                    int runLength = i - runStart;
                    if (runLength >= threshold)
                    {
                        findings.Add(Warning(
                            code,
                            $"{messagePrefix}: {runLength} levels from {levels[runStart].Id} to {levels[i - 1].Id}.",
                            path));
                    }

                    runStart = -1;
                }
            }
        }

        private static void AppendHeader(StringBuilder builder, LevelPacketManifest manifest)
        {
            builder.AppendLine($"Packet Design Report: {manifest.PacketId} - {manifest.DisplayName}");
            builder.AppendLine(new string('=', 26 + manifest.PacketId.Length + manifest.DisplayName.Length));
        }

        private static void AppendIdentity(StringBuilder builder, string manifestPath, string levelsDir, string briefsDir)
        {
            builder.AppendLine("Identity:");
            builder.AppendLine($"  Manifest: {manifestPath}");
            builder.AppendLine($"  Levels: {levelsDir}");
            builder.AppendLine($"  Briefs: {briefsDir}");
        }

        private static void AppendPresence(
            StringBuilder builder,
            LevelPacketManifest manifest,
            Dictionary<string, string> levelPathsById,
            Dictionary<string, string> briefPathsById)
        {
            builder.AppendLine("Presence:");
            builder.AppendLine($"  expected ids: {FormatIds(manifest.ExpectedLevelIds)}");
            builder.AppendLine($"  level ids present: {FormatIds(FilterPresent(manifest.ExpectedLevelIds, levelPathsById))}");
            builder.AppendLine($"  level ids missing: {FormatIds(FilterMissing(manifest.ExpectedLevelIds, levelPathsById))}");
            builder.AppendLine($"  brief ids present: {FormatIds(FilterPresent(manifest.ExpectedLevelIds, briefPathsById))}");
            builder.AppendLine($"  brief ids missing: {FormatIds(FilterMissing(manifest.ExpectedLevelIds, briefPathsById))}");
            builder.AppendLine($"  extra level ids: {FormatIds(FilterExtras(manifest.ExpectedLevelIds, levelPathsById))}");
            builder.AppendLine($"  extra brief ids: {FormatIds(FilterExtras(manifest.ExpectedLevelIds, briefPathsById))}");
        }

        private static void AppendProgression(StringBuilder builder, IReadOnlyList<PacketLevelDesignInfo> levels)
        {
            builder.AppendLine("Sequence Metrics:");
            for (int i = 0; i < levels.Count; i++)
            {
                PacketLevelDesignInfo info = levels[i];
                LevelJson? level = info.Level;
                builder.Append("  ");
                builder.Append(info.Id);
                if (level is null)
                {
                    builder.AppendLine(": level=<missing or invalid>");
                    continue;
                }

                builder.Append($": size={level.Board.Width}x{level.Board.Height}");
                builder.Append($", targets={level.Targets.Length}");
                builder.Append($", debrisPool={level.DebrisTypePool.Length}");
                builder.Append($", waterInterval={level.Water.RiseInterval}");
                builder.Append($", floodedRows={level.InitialFloodedRows}");
                builder.Append($", assistance={FormatDouble(level.Assistance.Chance)}");
                builder.Append($", dockJam={level.Dock.JamEnabled}");
                builder.Append($", density={FormatPercent(info.Metrics?.VisualOccupancyRatio ?? 0.0d)}");
                builder.AppendLine();
            }
        }

        private static void AppendMechanicIntroductions(StringBuilder builder, IReadOnlyList<PacketLevelDesignInfo> levels)
        {
            Dictionary<string, int> introductions = BuildMechanicIntroductionIndices(levels);
            List<string> mechanics = new List<string>(introductions.Keys);
            mechanics.Sort(StringComparer.Ordinal);

            builder.AppendLine("Mechanic Introduction Order:");
            if (mechanics.Count == 0)
            {
                builder.AppendLine("  none");
                return;
            }

            for (int i = 0; i < mechanics.Count; i++)
            {
                string mechanic = mechanics[i];
                builder.AppendLine($"  {mechanic}: {levels[introductions[mechanic]].Id}");
            }
        }

        private static void AppendBriefSequences(StringBuilder builder, IReadOnlyList<PacketLevelDesignInfo> levels)
        {
            builder.AppendLine("Brief Sequences:");
            builder.AppendLine("  roles: " + FormatBriefSequence(levels, info => info.Brief?.Role));
            builder.AppendLine("  primarySkills: " + FormatBriefSequence(levels, info => info.Brief?.PrimarySkill));
        }

        private static void AppendWarningsByLevel(
            StringBuilder builder,
            LevelPacketManifest manifest,
            List<ValidationError> findings)
        {
            builder.AppendLine("Warnings by Level:");
            if (findings.Count == 0)
            {
                builder.AppendLine("  None flagged.");
                return;
            }

            string[] expectedIds = manifest.ExpectedLevelIds ?? Array.Empty<string>();
            for (int i = 0; i < expectedIds.Length; i++)
            {
                bool wroteHeader = false;
                for (int f = 0; f < findings.Count; f++)
                {
                    ValidationError finding = findings[f];
                    if (!MentionsLevel(finding, expectedIds[i]))
                    {
                        continue;
                    }

                    if (!wroteHeader)
                    {
                        builder.AppendLine($"  {expectedIds[i]}:");
                        wroteHeader = true;
                    }

                    AppendFinding(builder, finding, indent: "    ");
                }
            }

            bool wrotePacketHeader = false;
            for (int f = 0; f < findings.Count; f++)
            {
                ValidationError finding = findings[f];
                if (MentionsAnyLevel(finding, expectedIds))
                {
                    continue;
                }

                if (!wrotePacketHeader)
                {
                    builder.AppendLine("  packet:");
                    wrotePacketHeader = true;
                }

                AppendFinding(builder, finding, indent: "    ");
            }
        }

        private static void AppendFindingList(StringBuilder builder, string label, IReadOnlyList<ValidationError> findings)
        {
            builder.AppendLine(label);
            if (findings.Count == 0)
            {
                builder.AppendLine("  None flagged.");
                return;
            }

            for (int i = 0; i < findings.Count; i++)
            {
                AppendFinding(builder, findings[i], "  ");
            }
        }

        private static void AppendFinding(StringBuilder builder, ValidationError finding, string indent)
        {
            builder.AppendLine($"{indent}{finding.Severity}: {finding.Code} at {finding.Path}");
            builder.AppendLine($"{indent}  {finding.Message}");
        }

        private static Dictionary<string, string> GetLevelPathsById(string levelsDir, List<ValidationError> findings)
        {
            if (!Directory.Exists(levelsDir))
            {
                findings.Add(Error("packet.levelsDir.missing", $"Levels directory was not found at '{levelsDir}'.", "$"));
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            return GetPathsById(levelsDir, "*.json", fileName => Path.GetFileNameWithoutExtension(fileName));
        }

        private static Dictionary<string, string> GetBriefPathsById(string briefsDir, List<ValidationError> findings)
        {
            if (!Directory.Exists(briefsDir))
            {
                findings.Add(Error("packet.briefsDir.missing", $"Briefs directory was not found at '{briefsDir}'.", "$"));
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            return GetPathsById(briefsDir, "*.brief.json", fileName =>
            {
                string name = Path.GetFileName(fileName);
                return name.EndsWith(".brief.json", StringComparison.OrdinalIgnoreCase)
                    ? name[..^".brief.json".Length]
                    : Path.GetFileNameWithoutExtension(fileName);
            });
        }

        private static Dictionary<string, string> GetPathsById(string dir, string pattern, Func<string, string> idSelector)
        {
            string[] files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                paths[idSelector(files[i])] = files[i];
            }

            return paths;
        }

        private static void AddScopedFindings(List<ValidationError> findings, string id, ValidationResult result)
        {
            for (int i = 0; i < result.Errors.Count; i++)
            {
                ValidationError finding = result.Errors[i];
                findings.Add(finding with
                {
                    Message = $"Level '{id}': {finding.Message}",
                    Path = LevelPath(id, finding.Path),
                });
            }
        }

        private static bool HasAssistanceIncreaseJustification(LevelJson level, LevelBrief? brief)
        {
            return !string.IsNullOrWhiteSpace(level.Meta.Notes)
                || !string.IsNullOrWhiteSpace(brief?.DesignNotes);
        }

        private static string FormatBriefSequence(IReadOnlyList<PacketLevelDesignInfo> levels, Func<PacketLevelDesignInfo, string?> selector)
        {
            if (levels.Count == 0)
            {
                return "none";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < levels.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(levels[i].Id);
                builder.Append('=');
                builder.Append(Normalize(selector(levels[i])) ?? "<missing>");
            }

            return builder.ToString();
        }

        private static string[] FilterPresent(string[]? expectedIds, Dictionary<string, string> pathsById)
        {
            return FilterByPresence(expectedIds, pathsById, present: true);
        }

        private static string[] FilterMissing(string[]? expectedIds, Dictionary<string, string> pathsById)
        {
            return FilterByPresence(expectedIds, pathsById, present: false);
        }

        private static string[] FilterByPresence(string[]? expectedIds, Dictionary<string, string> pathsById, bool present)
        {
            string[] ids = expectedIds ?? Array.Empty<string>();
            List<string> results = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                if (pathsById.ContainsKey(ids[i]) == present)
                {
                    results.Add(ids[i]);
                }
            }

            return results.ToArray();
        }

        private static string[] FilterExtras(string[]? expectedIds, Dictionary<string, string> pathsById)
        {
            HashSet<string> expected = new HashSet<string>(expectedIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            List<string> extras = new List<string>();
            foreach ((string id, string _) in pathsById)
            {
                if (!expected.Contains(id))
                {
                    extras.Add(id);
                }
            }

            extras.Sort(StringComparer.OrdinalIgnoreCase);
            return extras.ToArray();
        }

        private static bool MentionsLevel(ValidationError finding, string levelId)
        {
            string levelPathPrefix = "$.levels." + levelId;
            if (finding.Path.StartsWith("$.levels.", StringComparison.Ordinal))
            {
                return finding.Path.StartsWith(levelPathPrefix, StringComparison.OrdinalIgnoreCase);
            }

            return finding.Message.IndexOf("'" + levelId + "'", StringComparison.OrdinalIgnoreCase) >= 0
                || finding.Message.IndexOf(" " + levelId + " ", StringComparison.OrdinalIgnoreCase) >= 0
                || finding.Message.StartsWith(levelId + " ", StringComparison.OrdinalIgnoreCase);
        }

        private static bool MentionsAnyLevel(ValidationError finding, string[] expectedIds)
        {
            for (int i = 0; i < expectedIds.Length; i++)
            {
                if (MentionsLevel(finding, expectedIds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(string[]? values, string value)
        {
            string[] entries = values ?? Array.Empty<string>();
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasErrors(IReadOnlyList<ValidationError> findings)
        {
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Severity == ValidationSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string FormatIds(string[]? ids)
        {
            string[] values = ids ?? Array.Empty<string>();
            return values.Length == 0 ? "none" : string.Join(", ", values);
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(double ratio)
        {
            return (ratio * 100.0d).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        private static string LevelPath(string levelId)
        {
            return "$.levels." + levelId;
        }

        private static string LevelPath(string levelId, string path)
        {
            string suffix = path;
            if (suffix.StartsWith("$.", StringComparison.Ordinal))
            {
                suffix = suffix[2..];
            }
            else if (suffix.StartsWith('$'))
            {
                suffix = suffix[1..];
            }

            if (suffix.StartsWith('.'))
            {
                suffix = suffix[1..];
            }

            return string.IsNullOrWhiteSpace(suffix)
                ? LevelPath(levelId)
                : LevelPath(levelId) + "." + suffix;
        }

        private static ValidationError Error(string code, string message, string path)
        {
            return new ValidationError(ValidationSeverity.Error, code, message, path);
        }

        private static ValidationError Warning(string code, string message, string path)
        {
            return new ValidationError(ValidationSeverity.Warning, code, message, path);
        }

        private sealed record PacketLevelDesignInfo(
            int Index,
            string Id,
            LevelJson? Level,
            LevelBrief? Brief,
            LevelReadabilityMetrics? Metrics,
            IReadOnlyCollection<string> DetectedMechanics);
    }

    public sealed record LevelPacketDesignReport(
        string PacketId,
        string Text,
        bool HasErrors,
        IReadOnlyList<ValidationError> Findings);
}
