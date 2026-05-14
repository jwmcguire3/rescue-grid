using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Rescue.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class SettingsMenuView : MonoBehaviour
    {
        private const string SpriteResourceRoot = "Rescue.Unity/UI/Settings/";
        private const string FontResourceRoot = "Rescue.Unity/UI/Fonts/";
        private const string DisplayFontResourcePath = FontResourceRoot + "Rye SDF";
        private const string BodyFontResourcePath = FontResourceRoot + "DM Sans SDF";

        private static readonly Color Cream = new Color(0.89f, 0.82f, 0.68f, 1f);
        private static readonly Color DarkInk = new Color(0.09f, 0.08f, 0.06f, 1f);
        private static readonly Color Teal = new Color(0.29f, 0.62f, 0.64f, 1f);
        private static readonly Color Amber = new Color(0.79f, 0.48f, 0.15f, 1f);
        private static readonly Color SliderBed = new Color(0.22f, 0.16f, 0.12f, 1f);
        private const float SliderControlHeight = 44f;
        private const float SliderBarHeight = 42f;
        private const float SliderHandleSize = 38f;
        private const float TopPlaqueHeight = 60f;
        private const float SettingsPlaqueHeight = 80f;
        private const float PanelContentLeft = 70f;
        private const float PanelContentWidth = 342f;

        [Header("Top Buttons")]
        [SerializeField] private Button? restartButton;
        [SerializeField] private Button? settingsButton;

        [Header("Panel")]
        [SerializeField] private GameObject? panelRoot;
        [SerializeField] private Button? resumeButton;
        [SerializeField] private Button? showTutorialButton;
        [SerializeField] private TMP_Dropdown? levelDropdown;

        [Header("Audio")]
        [SerializeField] private Slider? musicSlider;
        [SerializeField] private Slider? fxSlider;
        [SerializeField] private Slider? hapticsStrengthSlider;
        [SerializeField] private Toggle? muteMusicToggle;
        [SerializeField] private Toggle? muteFxToggle;
        [SerializeField] private Toggle? hapticsToggle;
        [SerializeField] private TextMeshProUGUI? musicValueLabel;
        [SerializeField] private TextMeshProUGUI? fxValueLabel;
        [SerializeField] private TextMeshProUGUI? hapticsStrengthValueLabel;
        [SerializeField] private CanvasGroup? hapticsStrengthGroup;

        [Header("Sprites")]
        [SerializeField] private Sprite? settingsBackgroundSprite;
        [SerializeField] private Sprite? restartButtonSprite;
        [SerializeField] private Sprite? settingsButtonSprite;
        [SerializeField] private Sprite? tealPlaqueSprite;
        [SerializeField] private Sprite? amberPlaqueSprite;
        [SerializeField] private Sprite? levelBoxSprite;
        [SerializeField] private Sprite? dropdownArrowSprite;
        [SerializeField] private Sprite? sliderBarSprite;
        [SerializeField] private Sprite? sliderHandleSprite;
        [SerializeField] private Sprite? emptyBoxSprite;
        [SerializeField] private Sprite? checkedBoxSprite;
        [SerializeField] private Sprite? tealPawSprite;
        [SerializeField] private Sprite? amberPawSprite;
        [SerializeField] private TMP_FontAsset? displayFontAsset;
        [SerializeField] private TMP_FontAsset? bodyFontAsset;

        private readonly List<TextMeshProUGUI> readableLabels = new List<TextMeshProUGUI>();

        private enum FontRole
        {
            Body,
            Display
        }

        public Button RestartButton => Require(restartButton, nameof(restartButton));

        public Button SettingsButton => Require(settingsButton, nameof(settingsButton));

        public GameObject PanelRoot => Require(panelRoot, nameof(panelRoot));

        public Button ResumeButton => Require(resumeButton, nameof(resumeButton));

        public Button ShowTutorialButton => Require(showTutorialButton, nameof(showTutorialButton));

        public TMP_Dropdown LevelDropdown => Require(levelDropdown, nameof(levelDropdown));

        public Slider MusicSlider => Require(musicSlider, nameof(musicSlider));

        public Slider FxSlider => Require(fxSlider, nameof(fxSlider));

        public Slider HapticsStrengthSlider => Require(hapticsStrengthSlider, nameof(hapticsStrengthSlider));

        public Toggle MuteMusicToggle => Require(muteMusicToggle, nameof(muteMusicToggle));

        public Toggle MuteFxToggle => Require(muteFxToggle, nameof(muteFxToggle));

        public Toggle HapticsToggle => Require(hapticsToggle, nameof(hapticsToggle));

        public IReadOnlyList<TextMeshProUGUI> ReadableLabels => readableLabels;

        public static SettingsMenuView CreateRuntime(SettingsMenuPresenter owner)
        {
            GameObject canvasObject = new GameObject(
                "SettingsMenuCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(SettingsMenuView));
            canvasObject.transform.SetParent(owner.transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(975f, 1536f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasObject.GetComponent<RectTransform>();
            Stretch(root);
            EnsureEventSystem();

            SettingsMenuView view = canvasObject.GetComponent<SettingsMenuView>();
            view.BuildDefaultHierarchy();
            return view;
        }

        public void EnsureBuilt()
        {
            if (restartButton is null || settingsButton is null || panelRoot is null)
            {
                BuildDefaultHierarchy();
            }
        }

        public void SetOpen(bool open)
        {
            PanelRoot.SetActive(open);
        }

        public void SetLevelChoices(IReadOnlyList<string> choices, string selectedChoice)
        {
            TMP_Dropdown dropdown = LevelDropdown;
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(choices));

            int selectedIndex = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i], selectedChoice, StringComparison.Ordinal))
                {
                    selectedIndex = i;
                    break;
                }
            }

            dropdown.SetValueWithoutNotify(selectedIndex);
            dropdown.RefreshShownValue();
        }

        public string GetSelectedLevelChoice()
        {
            TMP_Dropdown dropdown = LevelDropdown;
            return dropdown.options.Count == 0 ? string.Empty : dropdown.options[Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1)].text;
        }

        public void SetMusicValue(float value, string label)
        {
            MusicSlider.SetValueWithoutNotify(value);
            Require(musicValueLabel, nameof(musicValueLabel)).text = label;
        }

        public void SetFxValue(float value, string label)
        {
            FxSlider.SetValueWithoutNotify(value);
            Require(fxValueLabel, nameof(fxValueLabel)).text = label;
        }

        public void SetHapticsStrengthValue(float value, string label)
        {
            HapticsStrengthSlider.SetValueWithoutNotify(value);
            Require(hapticsStrengthValueLabel, nameof(hapticsStrengthValueLabel)).text = label;
        }

        public void SetToggleValues(bool musicMuted, bool fxMuted, bool hapticsEnabled)
        {
            MuteMusicToggle.SetIsOnWithoutNotify(musicMuted);
            MuteFxToggle.SetIsOnWithoutNotify(fxMuted);
            HapticsToggle.SetIsOnWithoutNotify(hapticsEnabled);
            HapticsStrengthSlider.interactable = hapticsEnabled;
            if (hapticsStrengthGroup is not null)
            {
                hapticsStrengthGroup.alpha = hapticsEnabled ? 1.0f : 0.45f;
                hapticsStrengthGroup.interactable = hapticsEnabled;
            }
        }

        private void BuildDefaultHierarchy()
        {
            CacheSprites();
            CacheFonts();
            readableLabels.Clear();

            RectTransform root = EnsureRectTransform(gameObject);
            Stretch(root);

            GameObject anchorObject = CreateChild("SettingsMenuAnchor", root);
            RectTransform anchor = anchorObject.GetComponent<RectTransform>();
            anchor.anchorMin = new Vector2(1f, 1f);
            anchor.anchorMax = new Vector2(1f, 1f);
            anchor.pivot = new Vector2(1f, 1f);
            anchor.anchoredPosition = new Vector2(-36f, -56f);
            anchor.sizeDelta = new Vector2(480f, 820f);

            GameObject topRow = CreateChild("SettingsTopButtonRow", anchor);
            RectTransform topRowRect = topRow.GetComponent<RectTransform>();
            topRowRect.anchorMin = new Vector2(1f, 1f);
            topRowRect.anchorMax = new Vector2(1f, 1f);
            topRowRect.pivot = new Vector2(1f, 1f);
            topRowRect.anchoredPosition = new Vector2(18f, -40f);
            topRowRect.sizeDelta = new Vector2(220f, 156f);
            VerticalLayoutGroup topLayout = topRow.AddComponent<VerticalLayoutGroup>();
            topLayout.childAlignment = TextAnchor.UpperRight;
            topLayout.spacing = 8f;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = false;

            settingsButton = CreatePlaqueButton(topRow.transform, "SettingsButton", "SETTINGS", settingsButtonSprite, Cream, DarkInk, new Vector2(188f, SettingsPlaqueHeight), amberPawSprite);
            restartButton = CreatePlaqueButton(topRow.transform, "RestartButton", "RESTART", restartButtonSprite, Teal, Cream, new Vector2(208f, TopPlaqueHeight), tealPawSprite);

            panelRoot = CreateChild("SettingsPanel", root);
            RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(482f, 742f);

            Image panelImage = panelRoot.AddComponent<Image>();
            if (settingsBackgroundSprite is not null)
            {
                panelImage.sprite = settingsBackgroundSprite;
            }

            panelImage.type = GetImageType(settingsBackgroundSprite);
            panelImage.color = settingsBackgroundSprite is null ? new Color(0.08f, 0.12f, 0.14f, 0.96f) : Color.white;

            TextMeshProUGUI title = CreateLabel(panelRoot.transform, "SettingsTitle", "SETTINGS", 42f, Cream, TextAlignmentOptions.MidlineLeft, FontRole.Display);
            title.fontStyle = FontStyles.Bold;
            SetPanelRect(title.rectTransform, PanelContentLeft, 54f, 260f, 58f);
            resumeButton = CreateCloseButton(panelRoot.transform);
            SetPanelRect((RectTransform)resumeButton.transform, 396f, 40f, 44f, 44f);
            showTutorialButton = CreatePlaqueButton(panelRoot.transform, "ShowTutorialButton", "SHOW TUTORIAL", amberPlaqueSprite, Amber, DarkInk, new Vector2(0f, 68f), amberPawSprite);
            SetPanelRect((RectTransform)showTutorialButton.transform, PanelContentLeft, 118f, PanelContentWidth, 66f);

            levelDropdown = CreateDropdown(panelRoot.transform);
            SetPanelRect((RectTransform)levelDropdown.transform.parent, PanelContentLeft, 214f, PanelContentWidth, 52f);

            TextMeshProUGUI audioLabel = CreateLabel(panelRoot.transform, "AudioLabel", "AUDIO", 26f, Teal, TextAlignmentOptions.MidlineLeft, FontRole.Display);
            audioLabel.fontStyle = FontStyles.Bold;
            SetPanelRect(audioLabel.rectTransform, PanelContentLeft, 286f, 126f, 38f);

            musicSlider = CreateSliderRow(panelRoot.transform, "Music", out musicValueLabel);
            SetPanelRect((RectTransform)musicSlider.transform.parent, PanelContentLeft, 340f, PanelContentWidth, 48f);
            fxSlider = CreateSliderRow(panelRoot.transform, "FX", out fxValueLabel);
            SetPanelRect((RectTransform)fxSlider.transform.parent, PanelContentLeft, 408f, PanelContentWidth, 48f);

            GameObject muteRow = CreateRow(panelRoot.transform, "MuteRow", 48f);
            SetPanelRect((RectTransform)muteRow.transform, PanelContentLeft, 478f, PanelContentWidth, 48f);
            muteMusicToggle = CreateToggle(muteRow.transform, "MuteMusicToggle", "Mute Music");
            muteFxToggle = CreateToggle(muteRow.transform, "MuteFxToggle", "Mute FX");

            hapticsToggle = CreateToggle(panelRoot.transform, "VibrationsToggle", "Vibrations");
            SetPanelRect((RectTransform)hapticsToggle.transform, PanelContentLeft, 544f, 190f, 46f);

            GameObject strengthRow = CreateRow(panelRoot.transform, "HapticsStrengthRow", 46f);
            SetPanelRect((RectTransform)strengthRow.transform, PanelContentLeft, 594f, PanelContentWidth, 48f);
            hapticsStrengthGroup = strengthRow.AddComponent<CanvasGroup>();
            TextMeshProUGUI strengthLabel = CreateLabel(strengthRow.transform, "HapticsStrengthLabel", "Strength", 23f, Cream, TextAlignmentOptions.MidlineLeft);
            strengthLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 92f;
            hapticsStrengthSlider = CreateSlider(strengthRow.transform, "HapticsStrengthSlider");
            LayoutElement hapticsSliderLayout = hapticsStrengthSlider.gameObject.AddComponent<LayoutElement>();
            hapticsSliderLayout.flexibleWidth = 1f;
            hapticsSliderLayout.minWidth = 178f;
            hapticsSliderLayout.minHeight = SliderControlHeight;
            hapticsSliderLayout.preferredHeight = SliderControlHeight;
            hapticsStrengthValueLabel = CreateLabel(strengthRow.transform, "HapticsStrengthValue", "100%", 22f, Cream, TextAlignmentOptions.MidlineRight);
            hapticsStrengthValueLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 60f;

            panelRoot.SetActive(false);
        }

        private void CacheSprites()
        {
            settingsBackgroundSprite ??= LoadSprite("settings_background");
            restartButtonSprite ??= LoadSprite("teal_sign_plaque");
            settingsButtonSprite ??= LoadSprite("amber_sign_plaque");
            tealPlaqueSprite ??= LoadSprite("teal_sign_plaque");
            amberPlaqueSprite ??= LoadSprite("dark_yellow_sign_plaque");
            levelBoxSprite ??= LoadSprite("level_box");
            dropdownArrowSprite ??= LoadSprite("dropdown_select_button");
            sliderBarSprite ??= LoadSprite("slider_bar");
            sliderHandleSprite ??= LoadSprite("slider_bar_icon");
            emptyBoxSprite ??= LoadSprite("empty_box");
            checkedBoxSprite ??= LoadSprite("checked_box");
            tealPawSprite ??= LoadSprite("teal_paw_icon");
            amberPawSprite ??= LoadSprite("amber_paw_icon");
        }

        private void CacheFonts()
        {
            displayFontAsset ??= Resources.Load<TMP_FontAsset>(DisplayFontResourcePath);
            bodyFontAsset ??= Resources.Load<TMP_FontAsset>(BodyFontResourcePath);
        }

        private Button CreatePlaqueButton(
            Transform parent,
            string name,
            string text,
            Sprite? background,
            Color fallbackBackground,
            Color textColor,
            Vector2 size,
            Sprite? icon)
        {
            GameObject buttonObject = CreateChild(name, parent);
            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            if (size.x > 0f)
            {
                layout.preferredWidth = size.x;
            }

            layout.preferredHeight = size.y;

            Image image = buttonObject.AddComponent<Image>();
            if (background is not null)
            {
                image.sprite = background;
            }

            image.type = Image.Type.Simple;
            image.color = background is null ? fallbackBackground : Color.white;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            HorizontalLayoutGroup layoutGroup = buttonObject.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.padding = new RectOffset(16, 16, 5, 5);
            layoutGroup.spacing = 5f;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            if (icon is not null)
            {
                Image iconImage = CreateImage(buttonObject.transform, $"{name}Icon", icon, Color.white);
                iconImage.preserveAspect = true;
                RectTransform iconRect = iconImage.rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                LayoutElement iconLayout = iconImage.gameObject.AddComponent<LayoutElement>();
                float iconSize = Mathf.Min(26f, size.y - 20f);
                iconLayout.preferredWidth = iconSize;
                iconLayout.preferredHeight = iconSize;
                iconLayout.minWidth = iconSize;
                iconLayout.minHeight = iconSize;
                iconLayout.flexibleWidth = 0f;
                iconLayout.flexibleHeight = 0f;
            }

            TextMeshProUGUI label = CreateLabel(buttonObject.transform, $"{name}Label", text, 22f, textColor, TextAlignmentOptions.Center, FontRole.Display);
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = Mathf.Clamp(size.y * 0.42f, 20f, 28f);
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.preferredHeight = size.y - 8f;
            return button;
        }

        private Button CreateCloseButton(Transform parent)
        {
            GameObject buttonObject = CreateChild("ResumeButton", parent);
            Image image = buttonObject.AddComponent<Image>();
            image.type = Image.Type.Simple;
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private TMP_Dropdown CreateDropdown(Transform parent)
        {
            GameObject row = CreateRow(parent, "LevelDropdownRow", 52f);

            GameObject dropdownObject = CreateChild("LevelDropdown", row.transform);
            LayoutElement dropdownLayout = dropdownObject.AddComponent<LayoutElement>();
            dropdownLayout.flexibleWidth = 1f;
            dropdownLayout.minWidth = PanelContentWidth;
            dropdownLayout.preferredWidth = PanelContentWidth;
            dropdownLayout.preferredHeight = 48f;

            Image image = dropdownObject.AddComponent<Image>();
            if (levelBoxSprite is not null)
            {
                image.sprite = levelBoxSprite;
            }

            image.type = Image.Type.Simple;
            image.color = levelBoxSprite is null ? new Color(0.12f, 0.14f, 0.14f, 1f) : Color.white;

            TMP_Dropdown dropdown = dropdownObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = image;
            dropdown.captionText = CreateLabel(dropdownObject.transform, "Caption", string.Empty, 22f, Cream, TextAlignmentOptions.MidlineLeft);
            dropdown.captionText.enableAutoSizing = true;
            dropdown.captionText.fontSizeMin = 15f;
            dropdown.captionText.fontSizeMax = 22f;
            dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform captionRect = dropdown.captionText.rectTransform;
            captionRect.anchorMin = new Vector2(0f, 0f);
            captionRect.anchorMax = new Vector2(1f, 1f);
            captionRect.offsetMin = new Vector2(28f, 0f);
            captionRect.offsetMax = new Vector2(-60f, 0f);

            Image arrow = CreateImage(dropdownObject.transform, "Arrow", dropdownArrowSprite, Color.white);
            RectTransform arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-32f, 0f);
            arrowRect.sizeDelta = new Vector2(24f, 24f);
            arrow.preserveAspect = true;

            BuildDropdownTemplate(dropdown);
            return dropdown;
        }

        private void BuildDropdownTemplate(TMP_Dropdown dropdown)
        {
            GameObject template = CreateChild("Template", dropdown.transform);
            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -2f);
            templateRect.sizeDelta = new Vector2(0f, 220f);
            Image templateImage = template.AddComponent<Image>();
            templateImage.color = new Color(0.08f, 0.10f, 0.11f, 0.98f);
            ScrollRect scrollRect = template.AddComponent<ScrollRect>();

            GameObject viewport = CreateChild("Viewport", template.transform);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.white;

            GameObject content = CreateChild("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 32f);
            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.childControlHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandWidth = true;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject item = CreateChild("Item", content.transform);
            Toggle itemToggle = item.AddComponent<Toggle>();
            Image itemBackground = item.AddComponent<Image>();
            itemBackground.color = new Color(1f, 1f, 1f, 0.08f);
            itemToggle.targetGraphic = itemBackground;
            item.AddComponent<LayoutElement>().preferredHeight = 32f;

            TextMeshProUGUI itemLabel = CreateLabel(item.transform, "Item Label", "Option", 18f, Cream, TextAlignmentOptions.MidlineLeft);
            RectTransform itemLabelRect = itemLabel.rectTransform;
            Stretch(itemLabelRect);
            itemLabelRect.offsetMin = new Vector2(10f, 0f);
            itemLabelRect.offsetMax = new Vector2(-10f, 0f);
            dropdown.itemText = itemLabel;

            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            dropdown.template = templateRect;
            template.SetActive(false);
        }

        private Slider CreateSliderRow(Transform parent, string labelText, out TextMeshProUGUI valueLabel)
        {
            GameObject row = CreateRow(parent, $"{labelText}SliderRow", 46f);
            TextMeshProUGUI label = CreateLabel(row.transform, $"{labelText}Label", labelText, 23f, Cream, TextAlignmentOptions.MidlineLeft);
            label.gameObject.AddComponent<LayoutElement>().preferredWidth = 92f;
            Slider slider = CreateSlider(row.transform, $"{labelText}Slider");
            LayoutElement sliderLayout = slider.gameObject.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1f;
            sliderLayout.minWidth = 190f;
            sliderLayout.minHeight = SliderControlHeight;
            sliderLayout.preferredHeight = SliderControlHeight;
            valueLabel = CreateLabel(row.transform, $"{labelText}Value", "100%", 22f, Cream, TextAlignmentOptions.MidlineRight);
            valueLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 64f;
            return slider;
        }

        private Slider CreateSlider(Transform parent, string name)
        {
            GameObject sliderObject = CreateChild(name, parent);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(230f, SliderControlHeight);
            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            Image background = CreateImage(sliderObject.transform, "Background", sliderBarSprite, SliderBed);
            background.type = Image.Type.Simple;
            background.color = Color.clear;
            background.raycastTarget = true;
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(0f, SliderBarHeight);

            GameObject fillArea = CreateChild("Fill Area", sliderObject.transform);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(7f, 0f);
            fillAreaRect.offsetMax = new Vector2(-7f, 0f);

            Image fill = CreateImage(fillArea.transform, "Fill", sliderBarSprite, Teal);
            fill.type = Image.Type.Simple;
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(1f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, SliderBarHeight);

            GameObject handleArea = CreateChild("Handle Slide Area", sliderObject.transform);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            Stretch(handleAreaRect);
            handleAreaRect.offsetMin = new Vector2(12f, 0f);
            handleAreaRect.offsetMax = new Vector2(-12f, 0f);

            Image handle = CreateImage(handleArea.transform, "Handle", sliderHandleSprite, Cream);
            handle.preserveAspect = true;
            handle.raycastTarget = true;
            RectTransform handleRect = handle.rectTransform;
            handleRect.anchorMin = new Vector2(1f, 0.5f);
            handleRect.anchorMax = new Vector2(1f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(SliderHandleSize, SliderHandleSize);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            return slider;
        }

        private Toggle CreateToggle(Transform parent, string name, string labelText)
        {
            GameObject toggleObject = CreateChild(name, parent);
            LayoutElement layout = toggleObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.preferredHeight = 42f;
            HorizontalLayoutGroup group = toggleObject.AddComponent<HorizontalLayoutGroup>();
            group.childAlignment = TextAnchor.MiddleLeft;
            group.spacing = 8f;
            group.childControlHeight = true;
            group.childControlWidth = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            TextMeshProUGUI label = CreateLabel(toggleObject.transform, $"{name}Label", labelText, 22f, Cream, TextAlignmentOptions.MidlineLeft);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            GameObject boxObject = CreateChild($"{name}Box", toggleObject.transform);
            Image box = boxObject.AddComponent<Image>();
            if (emptyBoxSprite is not null)
            {
                box.sprite = emptyBoxSprite;
            }

            box.color = emptyBoxSprite is null ? new Color(0.10f, 0.10f, 0.10f, 1f) : Color.white;
            box.preserveAspect = true;
            LayoutElement boxLayout = boxObject.AddComponent<LayoutElement>();
            boxLayout.preferredWidth = 38f;
            boxLayout.preferredHeight = 38f;

            Image check = CreateImage(boxObject.transform, $"{name}Checkmark", checkedBoxSprite, Color.white);
            Stretch(check.rectTransform);

            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
        }

        private static Image.Type GetImageType(Sprite? sprite)
        {
            return sprite is not null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        }

        private static GameObject CreateRow(Transform parent, string name, float height)
        {
            GameObject row = CreateChild(name, parent);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        private TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            FontRole role = FontRole.Body)
        {
            GameObject labelObject = CreateChild(name, parent);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.color = color;
            label.fontSize = fontSize;
            label.font = ResolveFont(role);
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            readableLabels.Add(label);
            return label;
        }

        private TMP_FontAsset? ResolveFont(FontRole role)
        {
            return role == FontRole.Display ? displayFontAsset : bodyFontAsset;
        }

        private static Image CreateImage(Transform parent, string name, Sprite? sprite, Color fallbackColor)
        {
            GameObject imageObject = CreateChild(name, parent);
            Image image = imageObject.AddComponent<Image>();
            if (sprite is not null)
            {
                image.sprite = sprite;
            }

            image.color = sprite is null ? fallbackColor : Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static RectTransform EnsureRectTransform(GameObject target)
        {
            RectTransform? rect = target.GetComponent<RectTransform>();
            if (rect is not null)
            {
                return rect;
            }

            GameObject wrapper = new GameObject($"{target.name}RectRoot", typeof(RectTransform));
            wrapper.transform.SetParent(target.transform, false);
            return wrapper.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetPanelRect(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Sprite? LoadSprite(string name)
        {
            return Resources.Load<Sprite>(SpriteResourceRoot + name);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current is not null || FindAnyObjectByType<EventSystem>() is not null)
            {
                return;
            }

            _ = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static T Require<T>(T? value, string fieldName)
            where T : UnityEngine.Object
        {
            if (value is null)
            {
                throw new InvalidOperationException($"{nameof(SettingsMenuView)} is missing required reference '{fieldName}'.");
            }

            return value;
        }
    }
}
