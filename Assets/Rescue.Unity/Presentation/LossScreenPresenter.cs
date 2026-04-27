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

        [SerializeField] private Texture2D? lossImage;

        private UIDocument? document;
        private VisualElement? overlayRoot;
        private Label? explanationLabel;
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
                ActionOutcome.LossDockOverflow => "Dock overflow: too many pieces were left after clears.",
                ActionOutcome.LossWaterOnTarget => "Water reached an unrescued puppy.",
                ActionOutcome.LossDistressedExpired => "Distressed puppy was not rescued before the next water check.",
                _ => "Rescue stalled.",
            };

            if (explanationLabel is not null)
            {
                explanationLabel.text = explanationText;
            }

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

            Image image = new Image
            {
                name = "loss-screen-image",
                image = ResolveLossImage(),
                scaleMode = ScaleMode.ScaleToFit,
            };
            image.style.position = Position.Absolute;
            image.style.left = 0f;
            image.style.top = 0f;
            image.style.right = 0f;
            image.style.bottom = 0f;

            replayButton = CreateHitZoneButton("loss-replay-button", 7.5f, 87.5f, 35.0f, 8.5f);
            replayButton.clicked += RequestReplay;

            tryAgainButton = CreateHitZoneButton("loss-try-again-button", 48.0f, 87.5f, 44.0f, 8.5f);
            tryAgainButton.clicked += RequestTryAgain;

            explanationLabel = new Label(explanationText) { name = "loss-explanation-label" };
            explanationLabel.style.position = Position.Absolute;
            explanationLabel.style.left = Length.Percent(11.0f);
            explanationLabel.style.right = Length.Percent(11.0f);
            explanationLabel.style.top = Length.Percent(74.0f);
            explanationLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            explanationLabel.style.whiteSpace = WhiteSpace.Normal;
            explanationLabel.style.color = Color.white;
            explanationLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            explanationLabel.style.paddingTop = 8f;
            explanationLabel.style.paddingRight = 10f;
            explanationLabel.style.paddingBottom = 8f;
            explanationLabel.style.paddingLeft = 10f;

            overlayRoot.Add(image);
            overlayRoot.Add(explanationLabel);
            overlayRoot.Add(replayButton);
            overlayRoot.Add(tryAgainButton);
            root.Add(overlayRoot);
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

        private static Button CreateHitZoneButton(string name, float leftPercent, float topPercent, float widthPercent, float heightPercent)
        {
            Button button = new Button { name = name, text = string.Empty };
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
            return button;
        }
    }
}
