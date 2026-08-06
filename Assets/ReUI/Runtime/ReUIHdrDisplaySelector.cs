using Gui.MainMenu;
using UnityEngine;
using UnityEngine.UI;

namespace ReUI
{
    [DisallowMultipleComponent]
    internal sealed class ReUIHdrDisplaySelectorState : MonoBehaviour
    {
        internal Toggle Standard;
        internal Toggle Hdr;

        private void OnEnable()
        {
            ReUIHdrDisplaySettings.Changed += OnChanged;
            Refresh();
        }

        private void OnDisable()
        {
            ReUIHdrDisplaySettings.Changed -= OnChanged;
        }

        internal void Initialize()
        {
            Standard.onValueChanged.AddListener(value =>
            {
                if (value) ReUIHdrDisplaySettings.Enabled = false;
            });
            Hdr.onValueChanged.AddListener(value =>
            {
                if (value) ReUIHdrDisplaySettings.Enabled = true;
            });
            Refresh();
        }

        internal void Refresh()
        {
            bool enabled = ReUIHdrDisplaySettings.Enabled;
            if (Standard != null) Standard.SetIsOnWithoutNotify(!enabled);
            if (Hdr != null) Hdr.SetIsOnWithoutNotify(enabled);
            StyleToggle(Standard, !enabled);
            StyleToggle(Hdr, enabled);
        }

        private void OnChanged(bool _) => Refresh();

        private static void StyleToggle(Toggle toggle, bool selected)
        {
            if (toggle == null) return;
            Image image = toggle.targetGraphic as Image ?? toggle.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = ReUICanvasStyler.SurfaceSprite;
                image.type = Image.Type.Sliced;
                image.color = selected
                    ? ReUIPalette.WithAlpha(ReUIPalette.AccentCyan, 0.38f)
                    : ReUIPalette.WithAlpha(ReUIPalette.GlassSoft, 0.30f);
            }

            if (toggle.graphic is Image focus)
            {
                focus.sprite = ReUICanvasStyler.SurfaceSprite;
                focus.type = Image.Type.Sliced;
                focus.color = selected
                    ? ReUIPalette.WithAlpha(ReUIPalette.OutlineStrong, 0.44f)
                    : Color.clear;
            }

            Text label = toggle.GetComponentInChildren<Text>(true);
            if (label == null) return;
            label.gameObject.SetActive(true);
            label.enabled = true;
            label.color = selected ? ReUIPalette.TextPrimary : ReUIPalette.TextSecondary;
            label.transform.SetAsLastSibling();
        }
    }

    internal static class ReUIHdrDisplaySelector
    {
        private const string RowName = "ReUI HDR Display";

        internal static void EnsureForSettings(Canvas canvas)
        {
            if (canvas == null || canvas.gameObject.scene.name != "SettingsScene") return;
            SettingsGeneral general = canvas.GetComponentInChildren<SettingsGeneral>(true);
            if (general == null) return;

            ReUIHdrDisplaySelectorState existing = general.transform.Find(RowName)
                ?.GetComponent<ReUIHdrDisplaySelectorState>();
            if (existing != null)
            {
                existing.Refresh();
                return;
            }

            Font font = FindFont(general.transform);
            GameObject row = new(RowName, typeof(RectTransform), typeof(LayoutElement),
                typeof(HorizontalLayoutGroup), typeof(ReUIHdrDisplaySelectorState));
            row.transform.SetParent(general.transform, false);

            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.minHeight = 72f;
            rowLayout.preferredHeight = 72f;
            rowLayout.flexibleWidth = 1f;

            HorizontalLayoutGroup horizontal = row.GetComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(12, 12, 4, 4);
            horizontal.spacing = 10f;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            Text title = CreateText(row.transform, "Title", "HDR 局部高亮", font, 26, TextAnchor.MiddleLeft);
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.minWidth = 220f;
            titleLayout.preferredWidth = 220f;

            ToggleGroup group = row.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;
            Toggle standard = CreateToggle(row.transform, "Standard", "标准", font, group);
            Toggle hdr = CreateToggle(row.transform, "HDR", "HDR", font, group);

            ReUIHdrDisplaySelectorState state = row.GetComponent<ReUIHdrDisplaySelectorState>();
            state.Standard = standard;
            state.Hdr = hdr;
            state.Initialize();
        }

        private static Toggle CreateToggle(Transform parent, string name, string label, Font font, ToggleGroup group)
        {
            GameObject root = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Toggle), typeof(LayoutElement), typeof(Outline));
            root.transform.SetParent(parent, false);
            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.minWidth = 160f;
            layout.preferredWidth = 190f;
            layout.flexibleWidth = 1f;

            Image surface = root.GetComponent<Image>();
            surface.sprite = ReUICanvasStyler.SurfaceSprite;
            surface.type = Image.Type.Sliced;
            Outline outline = root.GetComponent<Outline>();
            outline.effectColor = ReUIPalette.WithAlpha(ReUIPalette.Outline, 0.68f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            GameObject focusObject = new("Focus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            focusObject.transform.SetParent(root.transform, false);
            RectTransform focusRect = focusObject.GetComponent<RectTransform>();
            focusRect.anchorMin = Vector2.zero;
            focusRect.anchorMax = Vector2.one;
            focusRect.offsetMin = new Vector2(3f, 3f);
            focusRect.offsetMax = new Vector2(-3f, -3f);
            Image focus = focusObject.GetComponent<Image>();
            focus.raycastTarget = false;

            CreateText(root.transform, "Label", label, font, 24, TextAnchor.MiddleCenter).transform.SetAsLastSibling();
            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = surface;
            toggle.graphic = focus;
            toggle.group = group;
            return toggle;
        }

        private static Text CreateText(Transform parent, string name, string value, Font font, int size,
            TextAnchor alignment)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 4f);
            rect.offsetMax = new Vector2(-12f, -4f);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
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
