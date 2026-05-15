using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Rescue.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class TutorialCardPresenter : MonoBehaviour
    {
        private const string SpriteResourceRoot = "Rescue.Unity/UI/Tutorial/";
        private const string FontResourceRoot = "Rescue.Unity/UI/Fonts/";
        private const string DisplayFontResourcePath = FontResourceRoot + "Rye SDF";
        private const string BodyFontResourcePath = FontResourceRoot + "DM Sans SDF";
        private const string PlaceholderResourcePath = SpriteResourceRoot + "blank_placeholder";
        private const int CanvasSortingOrder = 1130;
        private const float PanelWidth = 680f;
        private const float PanelHeight = 910f;
        private const float MaxImageFrameWidth = 460f;
        private const float MaxImageFrameHeight = 330f;

        private static readonly Color Cream = new Color(0.89f, 0.82f, 0.68f, 1f);
        private static readonly Color Amber = new Color(0.88f, 0.58f, 0.22f, 1f);
        private static readonly Color DarkInk = new Color(0.09f, 0.08f, 0.06f, 1f);
        private static readonly Color FallbackPanel = new Color(0.08f, 0.13f, 0.15f, 0.98f);

        [SerializeField] private Texture2D? backgroundTexture;
        [SerializeField] private Texture2D? headerTexture;
        [SerializeField] private Texture2D? cardFrameTexture;
        [SerializeField] private Texture2D? continueButtonTexture;
        [SerializeField] private Texture2D? litIndicatorTexture;
        [SerializeField] private Texture2D? unlitIndicatorTexture;
        [SerializeField] private Texture2D? placeholderTexture;
        [SerializeField] private TMP_FontAsset? displayFontAsset;
        [SerializeField] private TMP_FontAsset? bodyFontAsset;

        private readonly Dictionary<Texture2D, Sprite> sprites = new Dictionary<Texture2D, Sprite>();
        private readonly List<Image> indicatorImages = new List<Image>();

        private GameObject? overlayRoot;
        private RectTransform? imageAreaRect;
        private Image? tutorialImage;
        private Image? frameImage;
        private TextMeshProUGUI? headerLabel;
        private TextMeshProUGUI? titleLabel;
        private TextMeshProUGUI? bodyLabel;
        private TextMeshProUGUI? pageLabel;
        private TutorialDeck? currentDeck;
        private int currentCardIndex;
        private bool isVisible;

        public event Action? Dismissed;

        public bool IsVisible => isVisible;

        public int CurrentCardIndex => currentCardIndex;

        public int CurrentCardCount => currentDeck?.cards.Length ?? 0;

        public string CurrentTitle => titleLabel?.text ?? string.Empty;

        public string CurrentBody => bodyLabel?.text ?? string.Empty;

        public string CurrentPageLabel => pageLabel?.text ?? string.Empty;

        public Texture2D? CurrentImageTexture => tutorialImage?.sprite?.texture;

        public static TutorialCardPresenter EnsureInstance()
        {
            TutorialCardPresenter? existing = FindAnyObjectByType<TutorialCardPresenter>();
            if (existing != null)
            {
                existing.EnsureBuilt();
                return existing;
            }

            GameObject host = new GameObject("TutorialCardPresenter");
            TutorialCardPresenter presenter = host.AddComponent<TutorialCardPresenter>();
            presenter.EnsureBuilt();
            return presenter;
        }

        public void ShowDeck(TutorialDeck deck)
        {
            if (deck.cards.Length == 0)
            {
                Hide();
                return;
            }

            EnsureBuilt();
            currentDeck = deck;
            currentCardIndex = 0;
            isVisible = true;
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(true);
            }

            RefreshCard();
        }

        public void Continue()
        {
            if (!isVisible || currentDeck is null)
            {
                return;
            }

            if (currentCardIndex < currentDeck.cards.Length - 1)
            {
                currentCardIndex++;
                RefreshCard();
                return;
            }

            Dismiss();
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

        public void Hide()
        {
            EnsureBuilt();
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            isVisible = false;
            currentDeck = null;
            currentCardIndex = 0;
        }

        private void Awake()
        {
            EnsureBuilt();
            Hide();
        }

        private void EnsureBuilt()
        {
            CacheAssets();
            if (overlayRoot != null)
            {
                return;
            }

            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(975f, 1536f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            RectTransform root = EnsureRectTransform(gameObject);
            Stretch(root);
            EnsureEventSystem();
            BuildHierarchy(root);
        }

        private void BuildHierarchy(RectTransform root)
        {
            overlayRoot = CreateChild("TutorialOverlay", root);
            RectTransform overlayRect = overlayRoot.GetComponent<RectTransform>();
            Stretch(overlayRect);
            Image scrim = overlayRoot.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.56f);
            Button scrimButton = overlayRoot.AddComponent<Button>();
            scrimButton.targetGraphic = scrim;
            scrimButton.onClick.AddListener(Continue);

            GameObject panel = CreateChild("TutorialPanel", overlayRect);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetSprite(backgroundTexture);
            panelImage.color = backgroundTexture is null ? FallbackPanel : Color.white;
            panelImage.preserveAspect = false;
            panelImage.raycastTarget = true;

            Image header = CreateImage(panel.transform, "TutorialHeaderPlaque", headerTexture, new Color(0.72f, 0.62f, 0.44f, 1f));
            SetRect(header.rectTransform, 145.5f, -12f, 389f, 68f);
            header.raycastTarget = false;

            headerLabel = CreateLabel(panel.transform, "TutorialHeaderLabel", string.Empty, 29f, DarkInk, TextAlignmentOptions.Center, FontRole.Display);
            headerLabel.fontStyle = FontStyles.Bold;
            SetRect(headerLabel.rectTransform, 178f, -1f, 324f, 48f);

            Button closeButton = CreateTransparentButton(panel.transform, "TutorialCloseButton", Dismiss);
            SetRect((RectTransform)closeButton.transform, 565f, 9f, 72f, 72f);

            GameObject imageArea = CreateChild("TutorialImageArea", panel.transform);
            imageAreaRect = imageArea.GetComponent<RectTransform>();
            SetRect(imageAreaRect, 110f, 132f, MaxImageFrameWidth, MaxImageFrameHeight);

            tutorialImage = CreateImage(imageArea.transform, "TutorialImage", placeholderTexture, Color.black);
            RectTransform tutorialImageRect = tutorialImage.rectTransform;
            tutorialImageRect.anchorMin = new Vector2(0.02f, 0.025f);
            tutorialImageRect.anchorMax = new Vector2(0.98f, 0.975f);
            tutorialImageRect.offsetMin = Vector2.zero;
            tutorialImageRect.offsetMax = Vector2.zero;
            tutorialImage.preserveAspect = true;

            frameImage = CreateImage(imageArea.transform, "TutorialCardFrame", cardFrameTexture, Color.white);
            Stretch(frameImage.rectTransform);
            frameImage.raycastTarget = false;

            titleLabel = CreateLabel(panel.transform, "TutorialTitleLabel", string.Empty, 38f, Amber, TextAlignmentOptions.MidlineLeft, FontRole.Display);
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.enableAutoSizing = true;
            titleLabel.fontSizeMin = 24f;
            titleLabel.fontSizeMax = 42f;
            SetRect(titleLabel.rectTransform, 60f, 520f, 480f, 56f);

            pageLabel = CreateLabel(panel.transform, "TutorialPageLabel", string.Empty, 24f, Cream, TextAlignmentOptions.MidlineRight, FontRole.Body);
            SetRect(pageLabel.rectTransform, 540f, 532f, 80f, 42f);

            Image underline = CreateSolidImage(panel.transform, "TutorialTitleUnderline", Amber);
            SetRect(underline.rectTransform, 60f, 579f, 560f, 3f);

            bodyLabel = CreateLabel(panel.transform, "TutorialBodyLabel", string.Empty, 28f, Cream, TextAlignmentOptions.TopLeft, FontRole.Body);
            bodyLabel.textWrappingMode = TextWrappingModes.Normal;
            bodyLabel.enableAutoSizing = true;
            bodyLabel.fontSizeMin = 18f;
            bodyLabel.fontSizeMax = 24f;
            bodyLabel.lineSpacing = 3f;
            SetRect(bodyLabel.rectTransform, 60f, 592f, 560f, 132f);

            Button continueButton = CreateButton(panel.transform, "TutorialContinueButton", continueButtonTexture, Continue);
            SetRect((RectTransform)continueButton.transform, 167f, 748f, 346f, 61f);

            GameObject indicators = CreateChild("TutorialIndicators", panel.transform);
            RectTransform indicatorsRect = indicators.GetComponent<RectTransform>();
            SetRect(indicatorsRect, 290f, 828f, 100f, 18f);
            HorizontalLayoutGroup indicatorLayout = indicators.AddComponent<HorizontalLayoutGroup>();
            indicatorLayout.childAlignment = TextAnchor.MiddleCenter;
            indicatorLayout.childControlHeight = true;
            indicatorLayout.childControlWidth = true;
            indicatorLayout.childForceExpandHeight = false;
            indicatorLayout.childForceExpandWidth = false;
            indicatorLayout.spacing = 6f;

            overlayRoot.SetActive(false);
        }

        private void RefreshCard()
        {
            if (currentDeck is null || currentDeck.cards.Length == 0)
            {
                return;
            }

            TutorialCard card = currentDeck.cards[Mathf.Clamp(currentCardIndex, 0, currentDeck.cards.Length - 1)];
            if (headerLabel != null)
            {
                headerLabel.text = card.header;
            }

            if (titleLabel != null)
            {
                titleLabel.text = card.title;
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = card.body;
            }

            if (pageLabel != null)
            {
                pageLabel.text = $"{currentCardIndex + 1} / {currentDeck.cards.Length}";
            }

            if (tutorialImage != null)
            {
                Texture2D? texture = ResolveCardTexture(card.imageResourcePath);
                ResizeImageFrame(texture);
                tutorialImage.sprite = GetSprite(texture);
                tutorialImage.color = texture is null ? Color.black : Color.white;
            }

            RefreshIndicators(currentDeck.cards.Length);
        }

        private void RefreshIndicators(int cardCount)
        {
            Transform? parent = indicatorImages.Count > 0 ? indicatorImages[0].transform.parent : transform.Find("TutorialOverlay/TutorialPanel/TutorialIndicators");
            if (parent == null)
            {
                return;
            }

            while (indicatorImages.Count < cardCount)
            {
                Image image = CreateImage(parent, $"TutorialIndicator{indicatorImages.Count + 1}", unlitIndicatorTexture, Color.gray);
                LayoutElement layout = image.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 15f;
                layout.preferredHeight = 15f;
                layout.minWidth = 15f;
                layout.minHeight = 15f;
                image.rectTransform.sizeDelta = new Vector2(15f, 15f);
                image.preserveAspect = true;
                indicatorImages.Add(image);
            }

            for (int i = 0; i < indicatorImages.Count; i++)
            {
                bool active = i < cardCount;
                indicatorImages[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                Texture2D? texture = i == currentCardIndex ? litIndicatorTexture : unlitIndicatorTexture;
                indicatorImages[i].sprite = GetSprite(texture);
                indicatorImages[i].color = texture is null ? Color.gray : Color.white;
            }
        }

        private void ResizeImageFrame(Texture2D? texture)
        {
            if (imageAreaRect == null || texture == null)
            {
                return;
            }

            float aspect = Mathf.Max(0.01f, texture.width / (float)texture.height);
            float width = MaxImageFrameWidth;
            float height = width / aspect;
            if (height > MaxImageFrameHeight)
            {
                height = MaxImageFrameHeight;
                width = height * aspect;
            }

            imageAreaRect.sizeDelta = new Vector2(width, height);
            imageAreaRect.anchoredPosition = new Vector2((PanelWidth - width) * 0.5f, -132f);

            if (frameImage != null)
            {
                Stretch(frameImage.rectTransform);
            }
        }

        private Texture2D? ResolveCardTexture(string resourcePath)
        {
            Texture2D? texture = null;
            if (!string.IsNullOrWhiteSpace(resourcePath))
            {
                texture = Resources.Load<Texture2D>(resourcePath);
            }

            return texture ?? placeholderTexture ?? Resources.Load<Texture2D>(PlaceholderResourcePath);
        }

        private void CacheAssets()
        {
            backgroundTexture ??= LoadTexture("settings_background");
            headerTexture ??= LoadTexture("amber_header_plaque");
            cardFrameTexture ??= LoadTexture("card_frame");
            continueButtonTexture ??= LoadTexture("continue_button");
            litIndicatorTexture ??= LoadTexture("lit_indicator");
            unlitIndicatorTexture ??= LoadTexture("unlit_indicator");
            placeholderTexture ??= Resources.Load<Texture2D>(PlaceholderResourcePath);
            displayFontAsset ??= Resources.Load<TMP_FontAsset>(DisplayFontResourcePath);
            bodyFontAsset ??= Resources.Load<TMP_FontAsset>(BodyFontResourcePath);
        }

        private Texture2D? LoadTexture(string name)
        {
            return Resources.Load<Texture2D>(SpriteResourceRoot + name);
        }

        private Sprite? GetSprite(Texture2D? texture)
        {
            if (texture is null)
            {
                return null;
            }

            if (!sprites.TryGetValue(texture, out Sprite sprite))
            {
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprites.Add(texture, sprite);
            }

            return sprite;
        }

        private enum FontRole
        {
            Body,
            Display
        }

        private TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            FontRole role)
        {
            GameObject labelObject = CreateChild(name, parent);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = role == FontRole.Display ? displayFontAsset : bodyFontAsset;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private Button CreateButton(Transform parent, string name, Texture2D? texture, UnityEngine.Events.UnityAction onClick)
        {
            Image image = CreateImage(parent, name, texture, new Color(0.17f, 0.43f, 0.45f, 1f));
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            return button;
        }

        private static Button CreateTransparentButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = CreateChild(name, parent);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            return button;
        }

        private Image CreateImage(Transform parent, string name, Texture2D? texture, Color fallbackColor)
        {
            GameObject imageObject = CreateChild(name, parent);
            Image image = imageObject.AddComponent<Image>();
            image.sprite = GetSprite(texture);
            image.color = texture is null ? fallbackColor : Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateSolidImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = CreateChild(name, parent);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
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
            if (rect != null)
            {
                return rect;
            }

            return target.AddComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            _ = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
