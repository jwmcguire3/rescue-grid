using Rescue.Core.Pipeline;

namespace Rescue.Unity.Telemetry
{
    public static class TelemetryEventClassifier
    {
        public static bool IsDevOnly(ActionEvent actionEvent)
        {
            return actionEvent is DebugSpawnOverrideApplied;
        }
    }
}
