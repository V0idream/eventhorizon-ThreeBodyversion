using Gui.MainMenu;
using UnityEngine;
using UnityEngine.UI;

namespace ReUI
{
    /// <summary>
    /// Groups advanced visual options behind a single General-settings entry
    /// so the first settings page remains compact.
    /// </summary>
    internal static class ReUIExtendedDisplayMenu
    {
        private const string LauncherName = "ReUI Display Extensions Button";
        private const string PanelName = "ReUI Display Extensions";

        internal static void EnsureForSettings(Canvas canvas)
        {
            if (canvas == null || canvas.gameObject.scene.name != "SettingsScene") return;

            SettingsGeneral general = canvas.GetComponentInChildren<SettingsGeneral>(true);
            if (general == null) return;

            Font font = FindFont(general.transform);
            Button launcher = general.transform.Find(LauncherName)?.GetComponent<Button>();
            if (launcher == null)
                launcher = CreateLauncher(general.transform, font);
            StyleButton(launcher);

            Canvas rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            Transform panel = rootCanvas.transform.Find(PanelName);
            if (panel == null)
                panel = CreatePanel(rootCanvas.transform, font);

            Transform content = panel.Find("Content");
            ReUIStatusBarSelector.EnsureIn(content, font);
            ReUIShieldStyleSelector.EnsureIn(content, font);
            ReUIHdrDisplaySelector.EnsureIn(content, font);
        }

        private static Button CreateLauncher(Transform parent, Font font)
        {
            GameObject root = new(LauncherName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button), typeof(LayoutElement), typeof(Outline));
            root.transform.SetParent(parent, false);

            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.minHeight = 72f;
            layout.preferredHeight = 72f;
            layout.flexibleWidth = 1f;

            Image image = root.GetComponent<Image>();
            image.sprite = ReUICanvasStyler.SurfaceSprite;
            image.type = Image.Type.Sliced;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                Canvas canvas = button.GetComponentInParent<Canvas>();
                Transform rootTransform = canvas != null && canvas.rootCanvas != null
                    ? canvas.rootCanvas.transform
                    : root.transform.root;
                Transform panel = rootTransform.Find(PanelName);
                if (panel == null) return;
                panel.gameObject.SetActive(true);
                panel.SetAsLastSibling();
            });

            CreateText(root.transform, "Label", "显示扩展", font, 26, TextAnchor.MiddleLeft,
                new Vector2(24f, 0f), new Vector2(0f, 0.5f), new Vector2(420f, 56f));
            Text hint = CreateText(root.transform, "Hint", "HDR · 护盾样式 · 十格状态条", font, 19,
                TextAnchor.MiddleRight, new Vector2(-24f, 0f), new Vector2(1f, 0.5f), new Vector2(520f, 48f));
            hint.color = ReUIPalette.TextSecondary;
            return button;
        }

        private static Transform CreatePanel(Transform parent, Font font)
        {
            GameObject panelObject = new(PanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(parent, false);
            panelObject.SetActive(false);

            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(1120f, 620f);

            Image surface = panelObject.GetComponent<Image>();
            surface.sprite = ReUICanvasStyler.SurfaceSprite;
            surface.type = Image.Type.Sliced;
            surface.color = ReUIPalette.WithAlpha(ReUIPalette.GlassElevated, 0.97f);
            surface.raycastTarget = true;

            Outline outline = panelObject.GetComponent<Outline>();
            outline.effectColor = ReUIPalette.WithAlpha(ReUIPalette.OutlineStrong, 0.86f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            CreateText(panel, "Title", "显示扩展", font, 32, TextAnchor.MiddleLeft,
                new Vector2(28f, -26f), new Vector2(0f, 1f), new Vector2(520f, 64f));

            Button close = CreateButton(panel, "Close", "关闭", font, new Vector2(-28f, -28f),
                new Vector2(1f, 1f), new Vector2(120f, 48f));
            close.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
            close.onClick.AddListener(() => panelObject.SetActive(false));

            GameObject contentObject = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentObject.transform.SetParent(panel, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(28f, 36f);
            content.offsetMax = new Vector2(-28f, -100f);

            VerticalLayoutGroup vertical = contentObject.GetComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(0, 0, 8, 8);
            vertical.spacing = 18f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            return panel;
        }

        private static Button CreateButton(Transform parent, string name, string label, Font font,
            Vector2 position, Vector2 anchor, Vector2 size)
        {
            GameObject root = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button),
                typeof(Outline));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.sprite = ReUICanvasStyler.SurfaceSprite;
            image.type = Image.Type.Sliced;
            image.color = ReUIPalette.WithAlpha(ReUIPalette.GlassSoft, 0.56f);

            Outline outline = root.GetComponent<Outline>();
            outline.effectColor = ReUIPalette.WithAlpha(ReUIPalette.Outline, 0.72f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            CreateText(root.transform, "Label", label, font, 22, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(0.5f, 0.5f), size);
            return button;
        }

        private static void StyleButton(Button button)
        {
            if (button == null) return;
            Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = ReUICanvasStyler.SurfaceSprite;
                image.type = Image.Type.Sliced;
                image.color = ReUIPalette.WithAlpha(ReUIPalette.GlassSoft, 0.30f);
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = ReUIPalette.WithAlpha(ReUIPalette.Outline, 0.64f);
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = false;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = ReUIPalette.TextPrimary;
            colors.pressedColor = ReUIPalette.TextSecondary;
            colors.selectedColor = Color.white;
            colors.disabledColor = ReUIPalette.TextMuted;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static Text CreateText(Transform parent, string name, string value, Font font, int fontSize,
            TextAnchor alignment, Vector2 position, Vector2 anchor, Vector2 size)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x >= 0.99f ? 1f : anchor.x <= 0.01f ? 0f : 0.5f,
                anchor.y >= 0.99f ? 1f : anchor.y <= 0.01f ? 0f : 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.text = value;
            text.alignment = alignment;
            text.color = ReUIPalette.TextPrimary;
            text.raycastTarget = false;
            return text;
        }

        private static Font FindFont(Transform root)
        {
            Text text = root != null ? root.GetComponentInChildren<Text>(true) : null;
            return text != null && text.font != null
                ? text.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
