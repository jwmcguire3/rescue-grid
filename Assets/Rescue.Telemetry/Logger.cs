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
        private readonly StreamWriter _writer;
        private bool _disposed;

        public TelemetryConfig Config { get; }

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

            _writer = new StreamWriter(
                new FileStream(outputPath, FileMode.Append, FileAccess.Write, FileShare.Read),
                Encoding.UTF8);
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

                _writer.WriteLine(line);
                _writer.Flush();
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
                _writer.Dispose();
            }
        }
    }
}
