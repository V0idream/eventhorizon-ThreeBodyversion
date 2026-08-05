using Combat.Component.Unit;
using Gui.MainMenu;
using UnityEngine;
using UnityEngine.UI;

namespace ReUI
{
    [DisallowMultipleComponent]
    internal sealed class ReUIShieldStyleSelectorState : MonoBehaviour
    {
        internal Toggle Classic;
        internal Toggle Modern;

        private void OnEnable()
        {
            EnergyShieldVisualSettings.StyleChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            EnergyShieldVisualSettings.StyleChanged -= Refresh;
        }

        internal void Initialize()
        {
            Classic.onValueChanged.AddListener(selected =>
            {
                if (selected) EnergyShieldVisualSettings.Style = EnergyShieldVisualStyle.Classic;
            });
            Modern.onValueChanged.AddListener(selected =>
            {
                if (selected) EnergyShieldVisualSettings.Style = EnergyShieldVisualStyle.Modern;
            });
            Refresh();
        }

        internal void Refresh()
        {
            var modern = EnergyShieldVisualSettings.Style == EnergyShieldVisualStyle.Modern;
            if (Classic != null) Classic.SetIsOnWithoutNotify(!modern);
            if (Modern != null) Modern.SetIsOnWithoutNotify(modern);
            StyleToggle(Classic, !modern);
            StyleToggle(Modern, modern);
        }

        private static void StyleToggle(Toggle toggle, bool selected)
        {
            if (toggle == null) return;
            var image = toggle.targetGraphic as Image ?? toggle.GetComponent<Image>();
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

            var label = toggle.GetComponentInChildren<Text>(true);
            if (label == null) return;
            label.gameObject.SetActive(true);
            label.enabled = true;
            label.canvasRenderer.SetAlpha(1f);
            label.color = selected ? ReUIPalette.TextPrimary : ReUIPalette.TextSecondary;
            label.transform.SetAsLastSibling();
        }
    }

    internal static class ReUIShieldStyleSelector
    {
        private const string RowName = "ReUI Shield Style";

        internal static void Ensure(Transform settings)
        {
            if (settings == null) return;
            var general = settings.GetComponentInChildren<SettingsGeneral>(true);
            if (general == null) return;

            var existing = general.transform.Find(RowName)?.GetComponent<ReUIShieldStyleSelectorState>();
            if (existing != null)
            {
                existing.Refresh();
                return;
            }

            var font = FindFont(general.transform);
            var rowObject = new GameObject(RowName, typeof(RectTransform), typeof(LayoutElement),
                typeof(HorizontalLayoutGroup), typeof(ReUIShieldStyleSelectorState));
            rowObject.transform.SetParent(general.transform, false);

            var rowLayout = rowObject.GetComponent<LayoutElement>();
            rowLayout.minHeight = 72f;
            rowLayout.preferredHeight = 72f;
            rowLayout.flexibleWidth = 1f;

            var horizontal = rowObject.GetComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(12, 12, 4, 4);
            horizontal.spacing = 10f;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            var title = CreateText(rowObject.transform, "Title", "护盾样式", font, 26, TextAnchor.MiddleLeft);
            var titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.minWidth = 220f;
            titleLayout.preferredWidth = 220f;

            var group = rowObject.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;
            var classic = CreateToggle(rowObject.transform, "Classic", "传统", font, group);
            var modern = CreateToggle(rowObject.transform, "Modern", "现代", font, group);

            var state = rowObject.GetComponent<ReUIShieldStyleSelectorState>();
            state.Classic = classic;
            state.Modern = modern;
            state.Initialize();
        }

        internal static void EnsureForSettings(Canvas canvas)
        {
            if (canvas == null || canvas.gameObject.scene.name != "SettingsScene") return;
            var all = canvas.transform.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name != "Settings" || all[i].Find("Buttons") == null) continue;
                Ensure(all[i]);
                return;
            }
        }

        private static Toggle CreateToggle(Transform parent, string name, string label, Font font,
            ToggleGroup group)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Toggle), typeof(LayoutElement), typeof(Outline));
            root.transform.SetParent(parent, false);

            var layout = root.GetComponent<LayoutElement>();
            layout.minWidth = 160f;
            layout.preferredWidth = 190f;
            layout.flexibleWidth = 1f;

            var surface = root.GetComponent<Image>();
            surface.sprite = ReUICanvasStyler.SurfaceSprite;
            surface.type = Image.Type.Sliced;

            var outline = root.GetComponent<Outline>();
            outline.effectColor = ReUIPalette.WithAlpha(ReUIPalette.Outline, 0.68f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            var focusObject = new GameObject("Focus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            focusObject.transform.SetParent(root.transform, false);
            var focusRect = focusObject.GetComponent<RectTransform>();
            focusRect.anchorMin = Vector2.zero;
            focusRect.anchorMax = Vector2.one;
            focusRect.offsetMin = new Vector2(3f, 3f);
            focusRect.offsetMax = new Vector2(-3f, -3f);
            var focus = focusObject.GetComponent<Image>();
            focus.raycastTarget = false;

            var labelText = CreateText(root.transform, "Label", label, font, 24, TextAnchor.MiddleCenter);
            labelText.transform.SetAsLastSibling();

            var toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = surface;
            toggle.graphic = focus;
            toggle.group = group;
            toggle.toggleTransition = Toggle.ToggleTransition.Fade;
            return toggle;
        }

        private static Text CreateText(Transform parent, string name, string value, Font font, int size,
            TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 4f);
            rect.offsetMax = new Vector2(-12f, -4f);
            rect.localScale = Vector3.one;

            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.alignment = alignment;
            text.color = ReUIPalette.TextPrimary;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Font FindFont(Transform root)
        {
            var text = root != null ? root.GetComponentInChildren<Text>(true) : null;
            if (text != null && text.font != null)
                return text.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
