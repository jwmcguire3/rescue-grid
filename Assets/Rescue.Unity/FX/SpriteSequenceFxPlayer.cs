using System.Collections;
using UnityEngine;

namespace Rescue.Unity.FX
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteSequenceFxPlayer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer? spriteRenderer;
        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
        [SerializeField] private float secondsPerFrame = 0.06f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool destroyAfterPlayback = true;
        [SerializeField] private bool loop;
        [SerializeField] private int sortingOrder = 100;

        private Coroutine? playbackCoroutine;

        private void Awake()
        {
            spriteRenderer ??= GetComponent<SpriteRenderer>();
            ApplyRendererSettings();
            ApplyFrame(0);
        }

        private void OnEnable()
        {
            if (!playOnEnable)
            {
                return;
            }

            StartPlayback();
        }

        private void OnDisable()
        {
            if (playbackCoroutine is null)
            {
                return;
            }

            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }

        public void StartPlayback()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (playbackCoroutine is not null)
            {
                StopCoroutine(playbackCoroutine);
            }

            playbackCoroutine = StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            spriteRenderer ??= GetComponent<SpriteRenderer>();
            ApplyRendererSettings();

            if (frames.Length == 0)
            {
                playbackCoroutine = null;

                if (destroyAfterPlayback && !loop)
                {
                    Destroy(gameObject);
                }

                yield break;
            }

            do
            {
                for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                {
                    ApplyFrame(frameIndex);

                    if (frameIndex == frames.Length - 1 && !loop)
                    {
                        break;
                    }

                    yield return CreateFrameDelay();
                }
            }
            while (loop);

            playbackCoroutine = null;

            if (destroyAfterPlayback)
            {
                Destroy(gameObject);
            }
        }

        private YieldInstruction CreateFrameDelay()
        {
            return secondsPerFrame <= 0f
                ? new WaitForEndOfFrame()
                : new WaitForSeconds(secondsPerFrame);
        }

        private void ApplyFrame(int frameIndex)
        {
            if (spriteRenderer is null || frames.Length == 0)
            {
                return;
            }

            int clampedIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
            spriteRenderer.sprite = frames[clampedIndex];
        }

        private void ApplyRendererSettings()
        {
            if (spriteRenderer is null)
            {
                return;
            }

            spriteRenderer.sortingOrder = sortingOrder;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            spriteRenderer ??= GetComponent<SpriteRenderer>();
            ApplyRendererSettings();
            ApplyFrame(0);
        }
#endif
    }
}
