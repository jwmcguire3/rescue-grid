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
    public sealed class VictoryScreenPresenter : MonoBehaviour
    {
        private const string DefaultImageAssetPath = "Assets/Rescue.Unity/Art/Win/RescueRowRestoredVictory.png";
        private const string RuntimeThemeResourcePath = "Rescue.Unity/Debug/UnityDefaultRuntimeTheme";
        private const int PanelSortingOrder = 1100;

        [SerializeField] private Texture2D? victoryImage;
        [SerializeField] private MonoBehaviour? maeReactionHook;

        private UIDocument? document;
        private VisualElement? overlayRoot;
        private Label? headlineLabel;
        private Label? aftercareLabel;
        private Button? replayButton;
        private Button? nextLevelButton;
        private bool nextLevelAvailable = true;
        private bool isVisible;

        public event Action? ReplayRequested;

        public event Action? NextLevelRequested;

        public bool IsVisible => isVisible;

        public bool IsNextLevelAvailable => nextLevelAvailable;

        public string HeadlineText => headlineLabel?.text ?? "Rescue complete";

        public string AftercareText => aftercareLabel?.text ?? "Warm kennel check complete.";

        public static VictoryScreenPresenter EnsureInstance()
        {
            VictoryScreenPresenter? existing = FindFirstObjectByType<VictoryScreenPresenter>();
            if (existing is not null)
            {
                existing.EnsureDocument();
                return existing;
            }

            GameObject host = new GameObject("VictoryScreen");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            host.AddComponent<UIDocument>();
            VictoryScreenPresenter presenter = host.AddComponent<VictoryScreenPresenter>();
            presenter.EnsureDocument();
            return presenter;
        }

        public void SetVictoryImage(Texture2D image)
        {
            victoryImage = image;
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
            SetNextLevelAvailable(nextLevelAvailable);
            NotifyTerminalHook(ActionOutcome.Win);
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

        public void SetNextLevelAvailable(bool available)
        {
            nextLevelAvailable = available;
            if (nextLevelButton is null)
            {
                return;
            }

            nextLevelButton.SetEnabled(available);
            nextLevelButton.pickingMode = available ? PickingMode.Position : PickingMode.Ignore;
            nextLevelButton.style.opacity = available ? 1.0f : 0.45f;
        }

        public void RequestReplay()
        {
            ReplayRequested?.Invoke();
        }

        public void RequestNextLevel()
        {
            if (!nextLevelAvailable)
            {
                return;
            }

            NextLevelRequested?.Invoke();
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

            if (nextLevelButton is not null)
            {
                nextLevelButton.clicked -= RequestNextLevel;
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

            overlayRoot = new VisualElement { name = "victory-screen-root" };
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
                name = "victory-screen-image",
                image = ResolveVictoryImage(),
                scaleMode = ScaleMode.ScaleToFit,
            };
            image.style.position = Position.Absolute;
            image.style.left = 0f;
            image.style.top = 0f;
            image.style.right = 0f;
            image.style.bottom = 0f;

            headlineLabel = CreateOverlayLabel("victory-headline-label", "Rescue complete", 8.0f, 8.0f, 84.0f, 7.0f, 36);
            Label framingLabel = CreateOverlayLabel(
                "victory-framing-label",
                "Every puppy is out of danger.",
                10.0f,
                15.0f,
                80.0f,
                5.0f,
                20);

            aftercareLabel = CreateOverlayLabel(
                "victory-aftercare-card",
                "Aftercare: dry towel, warm kennel, Mae watching close.",
                12.0f,
                68.0f,
                76.0f,
                7.5f,
                18);
            aftercareLabel.style.backgroundColor = new Color(0.04f, 0.10f, 0.12f, 0.72f);
            aftercareLabel.style.paddingTop = 8f;
            aftercareLabel.style.paddingRight = 10f;
            aftercareLabel.style.paddingBottom = 8f;
            aftercareLabel.style.paddingLeft = 10f;

            replayButton = CreateHitZoneButton("victory-replay-button", "Replay", 7.5f, 87.5f, 35.0f, 8.5f);
            replayButton.clicked += RequestReplay;

            nextLevelButton = CreateHitZoneButton("victory-next-level-button", "Next Level", 48.0f, 87.5f, 44.0f, 8.5f);
            nextLevelButton.clicked += RequestNextLevel;

            overlayRoot.Add(image);
            overlayRoot.Add(headlineLabel);
            overlayRoot.Add(framingLabel);
            overlayRoot.Add(aftercareLabel);
            overlayRoot.Add(replayButton);
            overlayRoot.Add(nextLevelButton);
            root.Add(overlayRoot);
            SetNextLevelAvailable(nextLevelAvailable);
        }

        private Texture2D? ResolveVictoryImage()
        {
            if (victoryImage is not null)
            {
                return victoryImage;
            }

#if UNITY_EDITOR
            victoryImage = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultImageAssetPath);
#endif
            return victoryImage;
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

        private static Label CreateOverlayLabel(
            string name,
            string text,
            float leftPercent,
            float topPercent,
            float widthPercent,
            float heightPercent,
            int fontSize)
        {
            Label label = new Label(text) { name = name };
            label.style.position = Position.Absolute;
            label.style.left = Length.Percent(leftPercent);
            label.style.top = Length.Percent(topPercent);
            label.style.width = Length.Percent(widthPercent);
            label.style.height = Length.Percent(heightPercent);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = Color.white;
            label.style.fontSize = fontSize;
            return label;
        }

        private static Button CreateHitZoneButton(string name, string text, float leftPercent, float topPercent, float widthPercent, float heightPercent)
        {
            Button button = new Button { name = name, text = text };
            button.style.position = Position.Absolute;
            button.style.left = Length.Percent(leftPercent);
            button.style.top = Length.Percent(topPercent);
            button.style.width = Length.Percent(widthPercent);
            button.style.height = Length.Percent(heightPercent);
            button.style.backgroundColor = new Color(0.06f, 0.13f, 0.16f, 0.88f);
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 18;
            return button;
        }
    }
}
