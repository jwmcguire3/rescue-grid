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
        private const float VictoryImageWidth = 941.0f;
        private const float VictoryImageHeight = 1672.0f;

        [SerializeField] private Texture2D? victoryImage;
        [SerializeField] private MonoBehaviour? maeReactionHook;

        private UIDocument? document;
        private VisualElement? overlayRoot;
        private VisualElement? victoryFrame;
        private Button? replayButton;
        private Button? nextLevelButton;
        private bool nextLevelAvailable = true;
        private bool isVisible;

        public event Action? ReplayRequested;

        public event Action? NextLevelRequested;

        public bool IsVisible => isVisible;

        public bool IsNextLevelAvailable => nextLevelAvailable;

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

            victoryFrame = new VisualElement { name = "victory-frame" };
            victoryFrame.style.position = Position.Relative;
            victoryFrame.style.width = Length.Percent(100.0f);
            victoryFrame.style.height = Length.Percent(100.0f);
            victoryFrame.RegisterCallback<GeometryChangedEvent>(HandleVictoryFrameGeometryChanged);

            Image image = new Image
            {
                name = "victory-screen-image",
                image = ResolveVictoryImage(),
                scaleMode = ScaleMode.StretchToFill,
            };
            image.style.position = Position.Absolute;
            image.style.left = 0f;
            image.style.top = 0f;
            image.style.right = 0f;
            image.style.bottom = 0f;

            replayButton = CreateHitZoneButton("victory-replay-button", "Replay", 7.5f, 87.5f, 35.0f, 8.5f);
            replayButton.clicked += RequestReplay;

            nextLevelButton = CreateHitZoneButton("victory-next-level-button", "Next Level", 48.0f, 87.5f, 44.0f, 8.5f);
            nextLevelButton.clicked += RequestNextLevel;

            victoryFrame.Add(image);
            victoryFrame.Add(replayButton);
            victoryFrame.Add(nextLevelButton);
            overlayRoot.Add(victoryFrame);
            root.Add(overlayRoot);
            SetNextLevelAvailable(nextLevelAvailable);
        }

        private void HandleVictoryFrameGeometryChanged(GeometryChangedEvent evt)
        {
            if (victoryFrame is null || overlayRoot is null)
            {
                return;
            }

            float availableWidth = overlayRoot.resolvedStyle.width;
            float availableHeight = overlayRoot.resolvedStyle.height;
            if (availableWidth <= 0f || availableHeight <= 0f)
            {
                return;
            }

            float imageAspect = VictoryImageWidth / VictoryImageHeight;
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

            victoryFrame.style.width = frameWidth;
            victoryFrame.style.height = frameHeight;
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
