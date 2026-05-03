using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rescue.Unity.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class L00IntroImagePresenter : MonoBehaviour
    {
        private const string DefaultImageAssetPath = "Assets/Rescue.Unity/Art/Onboarding/L00IntroOpen4RescuePaths.png";
        private const string RuntimeImageResourcePath = "Rescue.Unity/Art/Onboarding/L00IntroOpen4RescuePaths";
        private const string RuntimeThemeResourcePath = "Rescue.Unity/Debug/UnityDefaultRuntimeTheme";
        private const int PanelSortingOrder = 1120;

        [SerializeField] private Texture2D? introImage;

        private UIDocument? document;
        private VisualElement? overlayRoot;
        private bool isVisible;

        public event Action? Dismissed;

        public bool IsVisible => isVisible;

        public static L00IntroImagePresenter EnsureInstance()
        {
            L00IntroImagePresenter? existing = FindAnyObjectByType<L00IntroImagePresenter>();
            if (existing is not null)
            {
                existing.EnsureDocument();
                return existing;
            }

            GameObject host = new GameObject("L00IntroImage");
            host.AddComponent<UIDocument>();
            L00IntroImagePresenter presenter = host.AddComponent<L00IntroImagePresenter>();
            presenter.EnsureDocument();
            return presenter;
        }

        public void SetIntroImage(Texture2D image)
        {
            introImage = image;
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

        public void Hide()
        {
            EnsureDocument();
            if (overlayRoot is not null)
            {
                overlayRoot.style.display = DisplayStyle.None;
            }

            isVisible = false;
        }

        public void Dismiss()
        {
            if (!isVisible)
            {
                return;
            }

            Hide();
            Dismissed?.Invoke();
        }

        private void Awake()
        {
            EnsureDocument();
            Hide();
        }

        private void OnDestroy()
        {
            if (overlayRoot is not null)
            {
                overlayRoot.UnregisterCallback<PointerDownEvent>(HandlePointerDown);
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

            overlayRoot = new VisualElement { name = "l00-intro-root" };
            overlayRoot.style.position = Position.Absolute;
            overlayRoot.style.left = 0f;
            overlayRoot.style.top = 0f;
            overlayRoot.style.right = 0f;
            overlayRoot.style.bottom = 0f;
            overlayRoot.style.alignItems = Align.Center;
            overlayRoot.style.justifyContent = Justify.Center;
            overlayRoot.style.backgroundColor = Color.black;
            overlayRoot.pickingMode = PickingMode.Position;
            overlayRoot.RegisterCallback<PointerDownEvent>(HandlePointerDown);

            Image image = new Image
            {
                name = "l00-intro-image",
                image = ResolveIntroImage(),
                scaleMode = ScaleMode.ScaleToFit,
            };
            image.style.position = Position.Absolute;
            image.style.left = 0f;
            image.style.top = 0f;
            image.style.right = 0f;
            image.style.bottom = 0f;
            image.pickingMode = PickingMode.Ignore;

            overlayRoot.Add(image);
            root.Add(overlayRoot);
        }

        private void HandlePointerDown(PointerDownEvent evt)
        {
            evt.StopImmediatePropagation();
            Dismiss();
        }

        private Texture2D? ResolveIntroImage()
        {
            if (introImage is not null)
            {
                return introImage;
            }

            introImage = Resources.Load<Texture2D>(RuntimeImageResourcePath);
            if (introImage is not null)
            {
                return introImage;
            }

#if UNITY_EDITOR
            introImage = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultImageAssetPath);
#endif
            return introImage;
        }
    }
}
