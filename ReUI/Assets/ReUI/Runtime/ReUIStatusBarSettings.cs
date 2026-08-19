using System;
using UnityEngine;
using UnityEngine.UI;

namespace ReUI
{
    internal static class ReUIStatusBarSettings
    {
        internal const string PreferenceKey = "ReUI.TenSegmentStatusBars";

        internal static event Action<bool> Changed;

        internal static bool TenSegments
        {
            get => PlayerPrefs.GetInt(PreferenceKey, 0) != 0;
            set
            {
                if (TenSegments == value) return;
                PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke(value);
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class ReUIStatusBarSelectorState : MonoBehaviour
    {
        internal Toggle Continuous;
        internal Toggle Segmented;

        private void OnEnable()
        {
            ReUIStatusBarSettings.Changed += OnChanged;
            Refresh();
        }

        private void OnDisable()
        {
            ReUIStatusBarSettings.Changed -= OnChanged;
        }

        internal void Initialize()
        {
            Continuous.onValueChanged.AddListener(selected =>
            {
                if (selected) ReUIStatusBarSettings.TenSegments = false;
            });
            Segmented.onValueChanged.AddListener(selected =>
            {
                if (selected) ReUIStatusBarSettings.TenSegments = true;
            });
            Refresh();
        }

        internal void Refresh()
        {
            bool segmented = ReUIStatusBarSettings.TenSegments;
            if (Continuous != null) Continuous.SetIsOnWithoutNotify(!segmented);
            if (Segmented != null) Segmented.SetIsOnWithoutNotify(segmented);
            StyleToggle(Continuous, !segmented);
            StyleToggle(Segmented, segmented);
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
            if (label != null)
                label.color = selected ? ReUIPalette.TextPrimary : ReUIPalette.TextSecondary;
        }
    }

    internal static class ReUIStatusBarSelector
    {
        private const string RowName = "ReUI Status Bar Style";

        internal static ReUIStatusBarSelectorState EnsureIn(Transform parent, Font font)
        {
            if (parent == null) return null;
            ReUIStatusBarSelectorState existing = parent.Find(RowName)?.GetComponent<ReUIStatusBarSelectorState>();
            if (existing != null)
            {
                existing.Refresh();
                return existing;
            }

            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject row = new(RowName, typeof(RectTransform), typeof(LayoutElement),
                typeof(HorizontalLayoutGroup), typeof(ReUIStatusBarSelectorState));
            row.transform.SetParent(parent, false);

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

            Text title = CreateText(row.transform, "Title", "十格状态条", font, 26, TextAnchor.MiddleLeft);
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.minWidth = 220f;
            titleLayout.preferredWidth = 220f;

            ToggleGroup group = row.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;
            Toggle continuous = CreateToggle(row.transform, "Continuous", "连续", font, group);
            Toggle segmented = CreateToggle(row.transform, "Segmented", "十格", font, group);

            Text description = CreateText(row.transform, "Description", "生命与护盾按 10 格显示", font, 18,
                TextAnchor.MiddleLeft);
            LayoutElement descriptionLayout = description.gameObject.AddComponent<LayoutElement>();
            descriptionLayout.minWidth = 260f;
            descriptionLayout.preferredWidth = 330f;
            descriptionLayout.flexibleWidth = 1f;
            description.color = ReUIPalette.TextSecondary;

            ReUIStatusBarSelectorState state = row.GetComponent<ReUIStatusBarSelectorState>();
            state.Continuous = continuous;
            state.Segmented = segmented;
            state.Initialize();
            return state;
        }

        private static Toggle CreateToggle(Transform parent, string name, string label, Font font, ToggleGroup group)
        {
            GameObject root = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle),
                typeof(LayoutElement), typeof(Outline));
            root.transform.SetParent(parent, false);
            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.minWidth = 145f;
            layout.preferredWidth = 170f;
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
    }
}
