using UnityEngine;

namespace Rescue.Unity.Presentation.Targets
{
    [DefaultExecutionOrder(-10)]
    public sealed class TargetSurfacePoseAdapter : MonoBehaviour
    {
        private string visualChildName = string.Empty;
        private Quaternion visualLocalRotation = Quaternion.identity;
        private Transform? visualChild;

        public static TargetSurfacePoseAdapter? Ensure(
            GameObject targetObject,
            string visualChildName,
            Quaternion visualLocalRotation)
        {
            if (targetObject is null || string.IsNullOrWhiteSpace(visualChildName))
            {
                return null;
            }

            Transform? visualChild = targetObject.transform.Find(visualChildName);
            if (visualChild is null)
            {
                return null;
            }

            TargetSurfacePoseAdapter adapter =
                targetObject.GetComponent<TargetSurfacePoseAdapter>() ??
                targetObject.AddComponent<TargetSurfacePoseAdapter>();
            adapter.visualChildName = visualChildName;
            adapter.visualLocalRotation = visualLocalRotation;
            adapter.visualChild = visualChild;
            adapter.ApplyNow();
            return adapter;
        }

        public void ApplyNow()
        {
            ResolveVisualChild();
            if (visualChild is null)
            {
                return;
            }

            visualChild.localRotation = visualLocalRotation;
        }

        private void LateUpdate()
        {
            ApplyNow();
        }

        private void ResolveVisualChild()
        {
            if (visualChild is not null || string.IsNullOrWhiteSpace(visualChildName))
            {
                return;
            }

            visualChild = transform.Find(visualChildName);
        }
    }
}
