using System.Collections.Generic;
using UnityEngine;

namespace Rescue.Unity.Debugging
{
    [CreateAssetMenu(fileName = "PuppyAnimationCatalog", menuName = "Rescue Grid/Debug/Puppy Animation Catalog")]
    public sealed class TargetPuppyAnimationCatalog : ScriptableObject
    {
        [SerializeField] private List<AnimationClip> clips = new List<AnimationClip>();

        public IReadOnlyList<AnimationClip> Clips => clips;
    }
}
