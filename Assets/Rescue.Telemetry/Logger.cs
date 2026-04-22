using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Rescue.Telemetry
{
    public sealed record TelemetryConfig(
        bool CaptureSnapshotsEnabled,
        int CaptureSnapshotEveryNActions)
    {
        public static TelemetryConfig DevDefaults { get; } = new TelemetryConfig(
            CaptureSnapshotsEnabled: true,
            CaptureSnapshotEveryNActions: 1);

        public static TelemetryConfig ProductionDefaults { get; } = new TelemetryConfig(
            CaptureSnapshotsEnabled: false,
            CaptureSnapshotEveryNActions: 1);
    }

    public sealed class TelemetryLogger : IDisposable
    {
        private readonly object _lock = new object();
        private readonly string _outputPath;
        private bool _disposed;

        public TelemetryConfig Config { get; }

        public string OutputPath => _outputPath;

        public TelemetryLogger(string outputPath, TelemetryConfig config)
        {
            if (outputPath is null)
            {
                throw new ArgumentNullException(nameof(outputPath));
            }

            Config = config ?? throw new ArgumentNullException(nameof(config));

            string? directory = Path.GetDirectoryName(outputPath);
            if (directory is not null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _outputPath = outputPath;
        }

        public void Append(ITelemetryEvent telemetryEvent)
        {
            if (telemetryEvent is null)
            {
                throw new ArgumentNullException(nameof(telemetryEvent));
            }

            string line = JsonSerializer.Serialize(
                telemetryEvent,
                typeof(ITelemetryEvent),
                TelemetryJsonConverter.OuterOptions);

            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(TelemetryLogger));
                }

                using StreamWriter writer = new StreamWriter(
                    new FileStream(_outputPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                    Encoding.UTF8);
                writer.WriteLine(line);
                writer.Flush();
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }
        }
    }
}
