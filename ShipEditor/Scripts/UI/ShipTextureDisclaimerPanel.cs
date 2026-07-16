using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShipEditor.UI
{
    /// <summary>
    /// Exact first-use consent page for the player artwork tools.  It is a
    /// lightweight overlay rather than the generic confirmation window so
    /// that the two required Chinese button labels are always visible.
    /// </summary>
    public sealed class ShipTextureDisclaimerPanel : MonoBehaviour
    {
        private Action _onAgree;

        public static void Open(ShipEditorWindow owner, Action onAgree)
        {
            if (owner == null) return;
            var canvas = owner.GetComponentInParent<Canvas>() ??
                         owner.transform.root.GetComponentInChildren<Canvas>(true);
            if (canvas == null) return;

            var root = new GameObject("ShipTextureDisclaimer", typeof(RectTransform),
                typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();
            var overlayCanvas = root.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 499;
            root.AddComponent<GraphicRaycaster>();

            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = root.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.78f);
            image.raycastTarget = true;
            var panel = root.AddComponent<ShipTextureDisclaimerPanel>();
            panel._onAgree = onAgree;
            panel.Build();
        }

        private void Build()
        {
            var boxObject = new GameObject("DisclaimerBox", typeof(RectTransform), typeof(Image));
            boxObject.transform.SetParent(transform, false);
            var box = (RectTransform)boxObject.transform;
            box.anchorMin = new Vector2(0.5f, 0.5f);
            box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            box.sizeDelta = new Vector2(760f, 310f);
            boxObject.GetComponent<Image>().color = new Color(0.015f, 0.08f, 0.14f, 0.98f);

            var warning = CreateText(box, "免责声明：本功能仅供自定义舰船使用，导入非法图片的行为与作者本人无关，用户自行承担全部责任", 25);
            warning.color = new Color(1f, 0.12f, 0.12f, 1f);
            warning.alignment = TextAnchor.MiddleCenter;
            warning.horizontalOverflow = HorizontalWrapMode.Wrap;
            warning.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect((RectTransform)warning.transform, new Vector2(0.08f, 0.36f),
                new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);

            CreateButton(box, "退出", new Vector2(0.08f, 0.08f), new Vector2(0.43f, 0.29f), Close);
            CreateButton(box, "同意并继续", new Vector2(0.57f, 0.08f), new Vector2(0.92f, 0.29f), Agree);
        }

        private void Agree()
        {
            var callback = _onAgree;
            Destroy(gameObject);
            callback?.Invoke();
        }

        private void Close()
        {
            Destroy(gameObject);
        }

        private static Text CreateText(Transform parent, string value, int fontSize)
        {
            var objectValue = new GameObject("Text", typeof(RectTransform), typeof(Text));
            objectValue.transform.SetParent(parent, false);
            var text = objectValue.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            return text;
        }

        private static void CreateButton(Transform parent, string value, Vector2 anchorMin,
            Vector2 anchorMax, UnityEngine.Events.UnityAction action)
        {
            var objectValue = new GameObject(value, typeof(RectTransform), typeof(Image), typeof(Button));
            objectValue.transform.SetParent(parent, false);
            var rect = (RectTransform)objectValue.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            objectValue.GetComponent<Image>().color = new Color(0.05f, 0.3f, 0.46f, 1f);
            objectValue.GetComponent<Button>().onClick.AddListener(action);

            var label = CreateText(objectValue.transform, value, 22);
            label.alignment = TextAnchor.MiddleCenter;
            SetRect((RectTransform)label.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
