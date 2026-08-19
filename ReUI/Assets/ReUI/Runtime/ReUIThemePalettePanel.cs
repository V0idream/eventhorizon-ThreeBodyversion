using System;
using UnityEngine;
using UnityEngine.UI;

namespace ReUI
{
    [DisallowMultipleComponent]
    internal sealed class ReUIThemePaletteState : MonoBehaviour
    {
        private bool _initialized;
        private bool _suppressChanges;
        private Color _draftColor;

        internal Image Surface;
        internal Image Preview;
        internal Outline SurfaceOutline;
        internal InputField HexInput;
        internal Slider Hue;
        internal ReUIThemeColorSquareGraphic ColorSquare;
        internal ReUIThemeColorSquareInput ColorInput;

        private void OnEnable()
        {
            ReUIPalette.ThemeChanged += OnThemeChanged;
            RefreshFromPalette();
        }

        private void OnDisable()
        {
            ReUIPalette.ThemeChanged -= OnThemeChanged;
        }

        private void OnDestroy()
        {
            if (Hue != null) Hue.onValueChanged.RemoveListener(OnHueChanged);
            if (ColorInput != null) ColorInput.SelectionChanged -= OnSquareChanged;
            if (HexInput != null) HexInput.onEndEdit.RemoveListener(OnHexEdited);
        }

        internal void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (Hue != null) Hue.onValueChanged.AddListener(OnHueChanged);
            if (ColorInput != null) ColorInput.SelectionChanged += OnSquareChanged;
            if (HexInput != null) HexInput.onEndEdit.AddListener(OnHexEdited);
            RefreshFromPalette();
        }

        internal void SetPreset(Color color)
        {
            SetDraftColor(color);
        }

        internal void ApplySelectedTheme()
        {
            if (!TryReadHexInput())
                return;
            ReUIPalette.SetThemeColor(_draftColor);
            ReUIBootstrap.RefreshTheme();
        }

        internal void ResetTheme()
        {
            ReUIPalette.ResetThemeColor();
            ReUIBootstrap.RefreshTheme();
            RefreshFromPalette();
        }

        internal void RefreshFromPalette()
        {
            Color color = ReUIPalette.ThemeColor;
            SetDraftColor(color);

            if (Surface != null) Surface.color = ReUIPalette.WithAlpha(ReUIPalette.GlassElevated, 0.95f);
            if (SurfaceOutline != null) SurfaceOutline.effectColor = ReUIPalette.WithAlpha(ReUIPalette.OutlineStrong, 0.86f);

            ReUIThemePalettePanel.RefreshLauncherForRoot(transform.root);
        }

        private void OnHueChanged(float hue)
        {
            if (!Application.isPlaying || _suppressChanges) return;

            float saturation = ColorInput != null ? ColorInput.Saturation : 0.75f;
            float brightness = ColorInput != null ? ColorInput.Brightness : 0.9f;
            SetDraftColor(Color.HSVToRGB(hue, saturation, brightness));
        }

        private void OnSquareChanged(float saturation, float brightness)
        {
            if (!Application.isPlaying || _suppressChanges || Hue == null) return;
            SetDraftColor(Color.HSVToRGB(Hue.value, saturation, brightness));
        }

        private void OnHexEdited(string _)
        {
            if (_suppressChanges) return;
            if (!TryReadHexInput())
                RefreshHexInput();
        }

        private bool TryReadHexInput()
        {
            if (HexInput == null) return true;
            string value = HexInput.text?.Trim();
            if (string.IsNullOrEmpty(value)) return false;
            if (!value.StartsWith("#", StringComparison.Ordinal)) value = "#" + value;
            if (!ColorUtility.TryParseHtmlString(value, out Color parsed)) return false;
            parsed.a = 1f;
            SetDraftColor(parsed);
            return true;
        }

        private void SetDraftColor(Color color)
        {
            color.a = 1f;
            _draftColor = color;
            Color.RGBToHSV(color, out float hue, out float saturation, out float brightness);

            _suppressChanges = true;
            if (Hue != null) Hue.SetValueWithoutNotify(hue);
            if (ColorSquare != null) ColorSquare.SetHue(hue);
            if (ColorInput != null) ColorInput.SetSelection(saturation, brightness);
            _suppressChanges = false;

            if (Preview != null) Preview.color = color;
            RefreshHexInput();
        }

        private void RefreshHexInput()
        {
            if (HexInput == null) return;
            _suppressChanges = true;
            HexInput.SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGB(_draftColor));
            _suppressChanges = false;
        }

        private void OnThemeChanged(Color _)
        {
            RefreshFromPalette();
        }
    }

    /// <summary>
    /// Settings-only local theme selector. The controls are created at runtime
    /// so no gameplay scenes, save files or database assets are changed.
    /// </summary>
    internal static class ReUIThemePalettePanel
    {
        private const string LauncherName = "ReUI Theme Palette Button";
        private const string PanelName = "ReUI Theme Palette";

        private static readonly Color[] Presets =
        {
            new(0.700f, 0.440f, 1.000f, 1f),
            new(0.360f, 0.760f, 1.000f, 1f),
            new(0.220f, 0.960f, 0.850f, 1f),
            new(0.290f, 0.900f, 0.480f, 1f),
            new(0.950f, 0.820f, 0.260f, 1f),
            new(1.000f, 0.550f, 0.250f, 1f),
            new(1.000f, 0.360f, 0.450f, 1f),
            new(1.000f, 0.420f, 0.780f, 1f),
        };

        internal static void Ensure(Canvas canvas, Transform settings)
        {
            if (canvas == null || settings == null || canvas.gameObject.scene.name != "SettingsScene") return;

            Canvas rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            Transform buttons = settings.Find("Buttons");
            if (buttons == null) return;

            Font font = FindUIFont(settings);
            Button launcher = buttons.Find(LauncherName)?.GetComponent<Button>();
            if (launcher == null)
                launcher = CreateLauncher(buttons, font);
            ConfigureLauncher(launcher, font);

            ReUIThemePaletteState state = rootCanvas.transform.Find(PanelName)?.GetComponent<ReUIThemePaletteState>();
            if (state == null)
                state = CreatePanel(rootCanvas.transform, font);
            state.RefreshFromPalette();
        }

        /// <summary>
        /// Minimal Settings-scene hook used by the Beta5-scoped bootstrap. The
        /// palette is the only runtime addition made to this scene; its existing
        /// buttons, colours, sprites and layout are left entirely authored.
        /// </summary>
        internal static void EnsureForSettings(Canvas canvas)
        {
            if (canvas == null || canvas.gameObject.scene.name != "SettingsScene") return;

            Transform settings = FindSettingsRoot(canvas.transform);
            if (settings != null)
                Ensure(canvas, settings);
        }

        internal static void RefreshLauncherForRoot(Transform root)
        {
            Transform settings = FindSettingsRoot(root);
            Button launcher = settings?.Find("Buttons/" + LauncherName)?.GetComponent<Button>();
            if (launcher != null)
                ConfigureLauncher(launcher, FindUIFont(settings));
        }

        private static Transform FindSettingsRoot(Transform root)
        {
            if (root == null) return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "Settings" && all[i].Find("Buttons") != null)
                    return all[i];
            }

            return null;
        }

        private static Button CreateLauncher(Transform parent, Font font)
        {
            GameObject buttonObject = new(LauncherName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 64f;
            layout.preferredHeight = 64f;
            layout.flexibleWidth = 1f;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(() =>
            {
                Canvas canvas = button.GetComponentInParent<Canvas>();
                Transform root = canvas != null && canvas.rootCanvas != null
                    ? canvas.rootCanvas.transform
                    : buttonObject.transform.root;
                Show(root);
            });
            CreateText(buttonObject.transform, "Label", "主题色", font, 28, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image swatch = CreateImage(buttonObject.transform, "Preview", new Vector2(-26f, 0f),
                new Vector2(1f, 0.5f), new Vector2(32f, 32f), ReUIPalette.ThemeColor);
            swatch.rectTransform.pivot = new Vector2(1f, 0.5f);
            swatch.sprite = ReUICanvasStyler.SurfaceSprite;
            swatch.type = Image.Type.Sliced;
            swatch.raycastTarget = false;
            return button;
        }

        private static void ConfigureLauncher(Button button, Font font)
        {
            if (button == null) return;

            Image image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = ReUICanvasStyler.SurfaceSprite;
                image.type = Image.Type.Sliced;
                image.color = ReUIPalette.WithAlpha(ReUIPalette.GlassSoft, 0.38f);
                Outline outline = image.GetComponent<Outline>();
                if (outline == null) outline = image.gameObject.AddComponent<Outline>();
                outline.effectColor = ReUIPalette.WithAlpha(ReUIPalette.Outline, 0.72f);
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = false;
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.font = font;
                label.text = "主题色";
                label.color = ReUIPalette.TextPrimary;
                label.fontStyle = FontStyle.Bold;
            }

            Image swatch = button.transform.Find("Preview")?.GetComponent<Image>();
            if (swatch != null) swatch.color = ReUIPalette.ThemeColor;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = ReUIPalette.WithAlpha(ReUIPalette.TextPrimary, 1f);
            colors.pressedColor = ReUIPalette.WithAlpha(ReUIPalette.TextSecondary, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = ReUIPalette.WithAlpha(ReUIPalette.TextMuted, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static void Show(Transform anyRoot)
        {
            if (anyRoot == null) return;
            ReUIThemePaletteState state = anyRoot.GetComponentInChildren<ReUIThemePaletteState>(true);
            if (state == null) return;
            state.gameObject.SetActive(true);
            state.transform.SetAsLastSibling();
            state.RefreshFromPalette();
        }

        private static ReUIThemePaletteState CreatePanel(Transform parent, Font font)
        {
            GameObject panelObject = new(PanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(ReUIThemePaletteState));
            panelObject.transform.SetParent(parent, false);
            panelObject.SetActive(false);
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1040f, 700f);
            panel.anchoredPosition = Vector2.zero;

            Image surface = panelObject.GetComponent<Image>();
            surface.sprite = ReUICanvasStyler.SurfaceSprite;
            surface.type = Image.Type.Sliced;
            surface.raycastTarget = true;

            Outline outline = panelObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            ReUIThemePaletteState state = panelObject.GetComponent<ReUIThemePaletteState>();
            state.Surface = surface;
            state.SurfaceOutline = outline;

            Button close = CreateActionButton(panel, "Close", "关闭", font, new Vector2(448f, 306f), new Vector2(92f, 42f));
            close.onClick.AddListener(() => panelObject.SetActive(false));

            CreateColorSquare(panel, state, new Vector2(-245f, -20f));
            CreateHueSlider(panel, state, new Vector2(40f, -20f));

            Image inspector = CreateImage(panel, "Theme Inspector", new Vector2(298f, -24f),
                new Vector2(0.5f, 0.5f), new Vector2(370f, 510f),
                ReUIPalette.WithAlpha(ReUIPalette.GlassSoft, 0.42f));
            inspector.sprite = ReUICanvasStyler.SurfaceSprite;
            inspector.type = Image.Type.Sliced;
            inspector.raycastTarget = false;
            Outline inspectorOutline = inspector.gameObject.AddComponent<Outline>();
            inspectorOutline.effectColor = ReUIPalette.WithAlpha(ReUIPalette.Outline, 0.55f);
            inspectorOutline.effectDistance = new Vector2(1f, -1f);
            inspectorOutline.useGraphicAlpha = false;

            Image preview = CreateImage(panel, "Preview", new Vector2(298f, 164f), new Vector2(0.5f, 0.5f),
                new Vector2(312f, 112f), ReUIPalette.ThemeColor);
            preview.sprite = ReUICanvasStyler.SurfaceSprite;
            preview.type = Image.Type.Sliced;
            Outline previewOutline = preview.gameObject.AddComponent<Outline>();
            previewOutline.effectColor = ReUIPalette.WithAlpha(Color.white, 0.84f);
            previewOutline.effectDistance = new Vector2(1f, -1f);
            previewOutline.useGraphicAlpha = false;
            state.Preview = preview;
            state.HexInput = CreateHexInput(preview.transform, font);
            CreateText(inspector.transform, "Hex Hint", "可编辑 HEX，输入后点击“应用主题”", font, 17,
                TextAnchor.MiddleCenter, new Vector2(0f, 83f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(330f, 34f), ReUIPalette.TextSecondary);

            for (int i = 0; i < Presets.Length; i++)
            {
                int column = i % 4;
                int row = i / 4;
                CreateSwatch(panel, "Preset " + i, Presets[i],
                    new Vector2(151f + column * 82f, 38f - row * 72f), state);
            }

            Button apply = CreateActionButton(panel, "Apply Theme", "应用主题", font,
                new Vector2(298f, -242f), new Vector2(312f, 54f));
            apply.onClick.AddListener(state.ApplySelectedTheme);
            Button reset = CreateActionButton(panel, "Reset", "恢复默认", font,
                new Vector2(298f, -302f), new Vector2(312f, 42f));
            reset.onClick.AddListener(state.ResetTheme);

            state.Initialize();
            return state;
        }

        private static void CreateColorSquare(Transform parent, ReUIThemePaletteState state, Vector2 position)
        {
            GameObject squareObject = new("Color Square", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(ReUIThemeColorSquareGraphic), typeof(ReUIThemeColorSquareInput));
            squareObject.transform.SetParent(parent, false);
            RectTransform rect = squareObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(500f, 500f);

            ReUIThemeColorSquareGraphic square = squareObject.GetComponent<ReUIThemeColorSquareGraphic>();
            square.raycastTarget = true;
            Outline squareOutline = squareObject.AddComponent<Outline>();
            squareOutline.effectColor = ReUIPalette.WithAlpha(Color.white, 0.76f);
            squareOutline.effectDistance = new Vector2(1f, -1f);
            squareOutline.useGraphicAlpha = false;

            ReUIThemeColorSquareInput input = squareObject.GetComponent<ReUIThemeColorSquareInput>();
            Image selector = CreateImage(squareObject.transform, "Selection", Vector2.zero, new Vector2(0.5f, 0.5f),
                new Vector2(30f, 30f), ReUIPalette.WithAlpha(Color.black, 0.12f));
            selector.sprite = ReUICanvasStyler.SurfaceSprite;
            selector.type = Image.Type.Sliced;
            selector.raycastTarget = false;
            Outline selectorOutline = selector.gameObject.AddComponent<Outline>();
            selectorOutline.effectColor = Color.white;
            selectorOutline.effectDistance = new Vector2(1.5f, -1.5f);
            selectorOutline.useGraphicAlpha = false;
            input.Selection = selector.rectTransform;

            state.ColorSquare = square;
            state.ColorInput = input;
        }

        private static void CreateHueSlider(Transform parent, ReUIThemePaletteState state, Vector2 position)
        {
            GameObject sliderObject = new("Hue", typeof(RectTransform), typeof(Slider), typeof(ReUIThemeHueStripGraphic));
            sliderObject.transform.SetParent(parent, false);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = position;
            sliderRect.sizeDelta = new Vector2(34f, 500f);

            ReUIThemeHueStripGraphic strip = sliderObject.GetComponent<ReUIThemeHueStripGraphic>();
            strip.SetVertical(true);
            // The strip itself is the slider's click surface. Keeping its
            // raycast target enabled allows both a tap and a drag anywhere
            // along the full hue bar, not just on the handle.
            strip.raycastTarget = true;
            Outline stripOutline = sliderObject.AddComponent<Outline>();
            stripOutline.effectColor = ReUIPalette.WithAlpha(Color.white, 0.70f);
            stripOutline.effectDistance = new Vector2(1f, -1f);
            stripOutline.useGraphicAlpha = false;

            GameObject handleAreaObject = new("Handle Slide Area", typeof(RectTransform));
            handleAreaObject.transform.SetParent(sliderObject.transform, false);
            RectTransform handleArea = handleAreaObject.GetComponent<RectTransform>();
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(0f, 10f);
            handleArea.offsetMax = new Vector2(0f, -10f);

            Image handle = CreateImage(handleArea, "Handle", Vector2.zero, new Vector2(0.5f, 0.5f),
                new Vector2(48f, 18f), ReUIPalette.TextPrimary);
            handle.sprite = ReUICanvasStyler.SurfaceSprite;
            handle.type = Image.Type.Sliced;
            Outline handleOutline = handle.gameObject.AddComponent<Outline>();
            handleOutline.effectColor = ReUIPalette.WithAlpha(Color.black, 0.88f);
            handleOutline.effectDistance = new Vector2(1f, -1f);
            handleOutline.useGraphicAlpha = false;

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.BottomToTop;
            slider.targetGraphic = handle;
            slider.handleRect = handle.rectTransform;
            state.Hue = slider;
        }

        private static void CreateSwatch(Transform parent, string name, Color color, Vector2 position,
            ReUIThemePaletteState state)
        {
            GameObject swatchObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            swatchObject.transform.SetParent(parent, false);
            RectTransform rect = swatchObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(54f, 54f);

            Image image = swatchObject.GetComponent<Image>();
            image.sprite = ReUICanvasStyler.SurfaceSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            Outline outline = swatchObject.AddComponent<Outline>();
            outline.effectColor = ReUIPalette.WithAlpha(Color.white, 0.70f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            Button button = swatchObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => state.SetPreset(color));
        }

        private static Button CreateActionButton(Transform parent, string name, string label, Font font, Vector2 position,
            Vector2 size)
        {
            GameObject buttonObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = ReUICanvasStyler.SurfaceSprite;
            image.type = Image.Type.Sliced;
            image.color = ReUIPalette.WithAlpha(ReUIPalette.GlassSoft, 0.62f);
            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = ReUIPalette.WithAlpha(ReUIPalette.Outline, 0.72f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            CreateText(buttonObject.transform, "Label", label, font, 20, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                ReUIPalette.TextPrimary, FontStyle.Bold);
            return button;
        }

        private static InputField CreateHexInput(Transform parent, Font font)
        {
            GameObject inputObject = new("Hex Input", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(InputField));
            inputObject.transform.SetParent(parent, false);
            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(250f, 52f);

            Image background = inputObject.GetComponent<Image>();
            background.sprite = ReUICanvasStyler.SurfaceSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0f, 0f, 0f, 0.28f);
            Outline outline = inputObject.AddComponent<Outline>();
            outline.effectColor = ReUIPalette.WithAlpha(Color.white, 0.72f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            Text value = CreateText(inputObject.transform, "Text", string.Empty, font, 24,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                Color.white, FontStyle.Bold);
            value.supportRichText = false;
            value.raycastTarget = false;

            InputField input = inputObject.GetComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = value;
            input.characterLimit = 9;
            input.contentType = InputField.ContentType.Standard;
            input.lineType = InputField.LineType.SingleLine;
            input.caretColor = Color.white;
            input.selectionColor = new Color(1f, 1f, 1f, 0.28f);
            return input;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 position, Vector2 anchor,
            Vector2 size, Color color)
        {
            GameObject imageObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(Transform parent, string name, string value, Font font, int fontSize,
            TextAnchor alignment, Vector2 position, Vector2 anchor, Vector2 offsetMin, Vector2 size,
            Color? color = null, FontStyle fontStyle = FontStyle.Normal)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            if (size == Vector2.zero)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(8f, 4f);
                rect.offsetMax = new Vector2(-8f, -4f);
            }
            else
            {
                rect.anchorMin = rect.anchorMax = anchor;
                rect.pivot = new Vector2(anchor.x <= 0.01f ? 0f : anchor.x >= 0.99f ? 1f : 0.5f, 0.5f);
                rect.anchoredPosition = position;
                rect.offsetMin = offsetMin;
                rect.sizeDelta = size;
            }
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            text.color = color ?? ReUIPalette.TextPrimary;
            text.fontStyle = fontStyle;
            return text;
        }

        private static Font FindUIFont(Transform settings)
        {
            Text existing = settings != null ? settings.GetComponentInChildren<Text>(true) : null;
            if (existing != null && existing.font != null) return existing.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
