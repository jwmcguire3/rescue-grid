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
        [SerializeField] private bool faceMainCamera = true;
        [SerializeField] private int sortingOrder = 100;

        private Coroutine? playbackCoroutine;
        private int currentFrameIndex;

        public int FrameCount => frames.Length;

        public int CurrentFrameIndex => currentFrameIndex;

        public bool IsPlaying => playbackCoroutine is not null;

        public bool DestroyAfterPlayback
        {
            get => destroyAfterPlayback;
            set => destroyAfterPlayback = value;
        }

        private void Awake()
        {
            spriteRenderer ??= GetComponent<SpriteRenderer>();
            ApplyCameraFacing();
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

            currentFrameIndex = 0;
            playbackCoroutine = StartCoroutine(PlaySequence(currentFrameIndex));
        }

        public void PlayFromCurrentFrame()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (playbackCoroutine is not null)
            {
                StopCoroutine(playbackCoroutine);
            }

            playbackCoroutine = StartCoroutine(PlaySequence(currentFrameIndex));
        }

        public void PausePlayback()
        {
            if (playbackCoroutine is null)
            {
                return;
            }

            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }

        public void StopPlayback()
        {
            PausePlayback();
            SetFrameIndex(0);
        }

        public void RestartPlayback()
        {
            SetFrameIndex(0);
            StartPlayback();
        }

        public void SetFrameIndex(int frameIndex)
        {
            if (frames.Length == 0)
            {
                currentFrameIndex = 0;
                return;
            }

            ApplyFrame(Mathf.Clamp(frameIndex, 0, frames.Length - 1));
        }

        public void NextFrame()
        {
            PausePlayback();
            if (frames.Length == 0)
            {
                currentFrameIndex = 0;
                return;
            }

            ApplyFrame((currentFrameIndex + 1) % frames.Length);
        }

        public void PreviousFrame()
        {
            PausePlayback();
            if (frames.Length == 0)
            {
                currentFrameIndex = 0;
                return;
            }

            ApplyFrame((currentFrameIndex - 1 + frames.Length) % frames.Length);
        }

        public void EnsureMinimumPlaybackDuration(float minimumDurationSeconds)
        {
            if (minimumDurationSeconds <= 0f || frames.Length <= 0)
            {
                return;
            }

            secondsPerFrame = Mathf.Max(secondsPerFrame, minimumDurationSeconds / frames.Length);
        }

        private IEnumerator PlaySequence(int startFrameIndex)
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
                int clampedStart = Mathf.Clamp(startFrameIndex, 0, frames.Length - 1);
                for (int frameIndex = clampedStart; frameIndex < frames.Length; frameIndex++)
                {
                    ApplyFrame(frameIndex);

                    if (frameIndex == frames.Length - 1 && !loop)
                    {
                        break;
                    }

                    yield return CreateFrameDelay();
                }

                startFrameIndex = 0;
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
            currentFrameIndex = clampedIndex;
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

        private void ApplyCameraFacing()
        {
            if (!faceMainCamera)
            {
                return;
            }

            Camera? mainCamera = Camera.main;
            if (mainCamera is null)
            {
                return;
            }

            transform.rotation = mainCamera.transform.rotation;
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
