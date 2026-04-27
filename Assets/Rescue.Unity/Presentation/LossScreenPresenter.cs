using System;
using System.Reflection;
using Rescue.Core.Pipeline;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rescue.Unity.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class LossScreenPresenter : MonoBehaviour
    {
        private const string DefaultImageAssetPath = "Assets/Rescue.Unity/Art/Loss/RescueStalledLoss.png";
        private const string RuntimeThemeResourcePath = "Rescue.Unity/Debug/UnityDefaultRuntimeTheme";
        private const int PanelSortingOrder = 1095;
        private const float LossImageWidth = 941.0f;
        private const float LossImageHeight = 1672.0f;

        [SerializeField] private Texture2D? lossImage;
        [SerializeField] private MonoBehaviour? maeReactionHook;

        private UIDocument? document;
        private VisualElement? overlayRoot;
        private VisualElement? lossFrame;
        private Button? replayButton;
        private Button? tryAgainButton;
        private bool isVisible;
        private string explanationText = "Rescue stalled.";

        public event Action? ReplayRequested;

        public event Action? TryAgainRequested;

        public bool IsVisible => isVisible;

        public string ExplanationText => explanationText;

        public static LossScreenPresenter EnsureInstance()
        {
            LossScreenPresenter? existing = FindFirstObjectByType<LossScreenPresenter>();
            if (existing is not null)
            {
                existing.EnsureDocument();
                return existing;
            }

            GameObject host = new GameObject("LossScreen");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            host.AddComponent<UIDocument>();
            LossScreenPresenter presenter = host.AddComponent<LossScreenPresenter>();
            presenter.EnsureDocument();
            return presenter;
        }

        public void SetLossImage(Texture2D image)
        {
            lossImage = image;
            if (overlayRoot is not null)
            {
                BuildVisualTree();
            }
        }

        public void Show()
        {
            EnsureDocument();
            if (overlayRoot is null)
            {
                return;
            }

            overlayRoot.style.display = DisplayStyle.Flex;
            isVisible = true;
        }

        public void Show(ActionOutcome outcome)
        {
            explanationText = outcome switch
            {
                ActionOutcome.LossDockOverflow => "Dock overflow.",
                ActionOutcome.LossWaterOnTarget => "Water reached a puppy.",
                ActionOutcome.LossDistressedExpired => "Distressed puppy was not rescued in time.",
                _ => "Rescue stalled.",
            };

            NotifyTerminalHook(outcome);
            Show();
        }

        public void Hide()
        {
            EnsureDocument();
            if (overlayRoot is not null)
            {
                overlayRoot.style.display = DisplayStyle.None;
            }

            isVisible = false;
        }

        public void RequestReplay()
        {
            ReplayRequested?.Invoke();
        }

        public void RequestTryAgain()
        {
            TryAgainRequested?.Invoke();
        }

        private void Awake()
        {
            EnsureDocument();
            Hide();
        }

        private void OnDestroy()
        {
            if (replayButton is not null)
            {
                replayButton.clicked -= RequestReplay;
            }

            if (tryAgainButton is not null)
            {
                tryAgainButton.clicked -= RequestTryAgain;
            }
        }

        private void EnsureDocument()
        {
            if (document is null)
            {
                document = GetComponent<UIDocument>();
                document.panelSettings = CreatePanelSettings();
            }

            if (overlayRoot is null)
            {
                BuildVisualTree();
            }
        }

        private PanelSettings CreatePanelSettings()
        {
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(975, 1536);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = PanelSortingOrder;
            settings.clearColor = false;
            settings.colorClearValue = Color.clear;
            ApplyRuntimeTheme(settings);
            return settings;
        }

        private static void ApplyRuntimeTheme(PanelSettings settings)
        {
            Type? themeStyleSheetType = Type.GetType("UnityEngine.UIElements.ThemeStyleSheet, UnityEngine.UIElementsModule");
            if (themeStyleSheetType is null)
            {
                return;
            }

            UnityEngine.Object? themeStyleSheet = Resources.Load(RuntimeThemeResourcePath, themeStyleSheetType);
            if (themeStyleSheet is null)
            {
                return;
            }

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type settingsType = settings.GetType();

            PropertyInfo? themeProperty = settingsType.GetProperty("themeStyleSheet", Flags)
                ?? settingsType.GetProperty("themeUss", Flags);
            if (themeProperty is not null && themeProperty.CanWrite && themeProperty.PropertyType.IsInstanceOfType(themeStyleSheet))
            {
                themeProperty.SetValue(settings, themeStyleSheet);
                return;
            }

            FieldInfo? themeField = settingsType.GetField("themeStyleSheet", Flags)
                ?? settingsType.GetField("themeUss", Flags);
            if (themeField is not null && themeField.FieldType.IsInstanceOfType(themeStyleSheet))
            {
                themeField.SetValue(settings, themeStyleSheet);
            }
        }

        private void BuildVisualTree()
        {
            if (document is null)
            {
                return;
            }

            VisualElement root = document.rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1.0f;
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;

            overlayRoot = new VisualElement { name = "loss-screen-root" };
            overlayRoot.style.position = Position.Absolute;
            overlayRoot.style.left = 0f;
            overlayRoot.style.top = 0f;
            overlayRoot.style.right = 0f;
            overlayRoot.style.bottom = 0f;
            overlayRoot.style.alignItems = Align.Center;
            overlayRoot.style.justifyContent = Justify.Center;
            overlayRoot.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);

            lossFrame = new VisualElement { name = "loss-frame" };
            lossFrame.style.position = Position.Relative;
            lossFrame.style.width = Length.Percent(100.0f);
            lossFrame.style.height = Length.Percent(100.0f);
            lossFrame.RegisterCallback<GeometryChangedEvent>(HandleLossFrameGeometryChanged);

            Image image = new Image
            {
                name = "loss-screen-image",
                image = ResolveLossImage(),
                scaleMode = ScaleMode.StretchToFill,
            };
            image.style.position = Position.Absolute;
            image.style.left = 0f;
            image.style.top = 0f;
            image.style.right = 0f;
            image.style.bottom = 0f;

            replayButton = CreateHitZoneButton("loss-replay-button", "Replay", 6.5f, 84.7f, 38.5f, 8.9f);
            replayButton.clicked += RequestReplay;

            tryAgainButton = CreateHitZoneButton("loss-try-again-button", "Retry", 47.5f, 84.7f, 43.5f, 8.9f);
            tryAgainButton.clicked += RequestTryAgain;

            lossFrame.Add(image);
            lossFrame.Add(replayButton);
            lossFrame.Add(tryAgainButton);
            overlayRoot.Add(lossFrame);
            root.Add(overlayRoot);
        }

        private void HandleLossFrameGeometryChanged(GeometryChangedEvent evt)
        {
            if (lossFrame is null || overlayRoot is null)
            {
                return;
            }

            float availableWidth = overlayRoot.resolvedStyle.width;
            float availableHeight = overlayRoot.resolvedStyle.height;
            if (availableWidth <= 0f || availableHeight <= 0f)
            {
                return;
            }

            float imageAspect = LossImageWidth / LossImageHeight;
            float availableAspect = availableWidth / availableHeight;
            float frameWidth = availableWidth;
            float frameHeight = availableHeight;
            if (availableAspect > imageAspect)
            {
                frameWidth = availableHeight * imageAspect;
            }
            else
            {
                frameHeight = availableWidth / imageAspect;
            }

            lossFrame.style.width = frameWidth;
            lossFrame.style.height = frameHeight;
        }

        private Texture2D? ResolveLossImage()
        {
            if (lossImage is not null)
            {
                return lossImage;
            }

#if UNITY_EDITOR
            lossImage = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultImageAssetPath);
#endif
            return lossImage;
        }

        private void NotifyTerminalHook(ActionOutcome outcome)
        {
            if (ResolveMaeReactionHook() is IMaeTerminalReactionHook terminalReactionHook)
            {
                terminalReactionHook.HandleTerminalOutcome(outcome);
            }
        }

        private MonoBehaviour? ResolveMaeReactionHook()
        {
            if (maeReactionHook is not null)
            {
                return maeReactionHook;
            }

            if (TryGetComponent(out MaeReactionPresenter existingPresenter))
            {
                maeReactionHook = existingPresenter;
                return maeReactionHook;
            }

            maeReactionHook = gameObject.AddComponent<MaeReactionPresenter>();
            return maeReactionHook;
        }

        private static Button CreateHitZoneButton(string name, string text, float leftPercent, float topPercent, float widthPercent, float heightPercent)
        {
            Button button = new Button { name = name, text = text };
            button.style.position = Position.Absolute;
            button.style.left = Length.Percent(leftPercent);
            button.style.top = Length.Percent(topPercent);
            button.style.width = Length.Percent(widthPercent);
            button.style.height = Length.Percent(heightPercent);
            button.style.backgroundColor = Color.clear;
            button.style.borderTopWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = 0f;
            button.style.color = Color.clear;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 18;
            return button;
        }
    }
}
