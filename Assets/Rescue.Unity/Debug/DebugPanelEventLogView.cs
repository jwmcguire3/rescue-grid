#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !CAPTURE_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rescue.Unity.Debugging
{
    internal static class DebugPanelEventLogView
    {
        public static void Render(VisualElement? list, IReadOnlyList<DebugActionLogEntry> entries)
        {
            if (list is null)
            {
                return;
            }

            list.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                list.Add(CreateLogEntryElement(entries[i]));
            }
        }

        private static VisualElement CreateLogEntryElement(DebugActionLogEntry entry)
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("event-log-entry");

            Label header = new Label($"{entry.ActionLabel} -> {entry.Outcome}");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(header);

            for (int i = 0; i < entry.Lines.Length; i++)
            {
                DebugEventLogLine line = entry.Lines[i];
                Label label = new Label(line.DevOnly ? $"[dev] {line.Message}" : line.Message);
                label.style.color = line.Color;
                container.Add(label);
            }

            return container;
        }
    }
}
#endif
