using System;
using System.IO;
using Constructor.Ships;
using Services.Gui;
using Services.Resources;
using UnityEngine;
using UnityEngine.UI;

namespace ShipEditor.UI
{
    /// <summary>
    /// A small self-contained artwork editor.  It intentionally lives above
    /// the normal ship editor as a modal page so component placement remains
    /// untouched while a player paints or adds a sticker.
    /// </summary>
    public sealed class ShipTextureCustomizationPanel : MonoBehaviour
    {
        private ShipEditorWindow _owner;
        private Sprite _baseSprite;
        private bool _sticker;
        private Texture2D _overlay;
        private Texture2D _preview;
        private RawImage _previewImage;
        private Slider _scale;
        private Slider _offsetX;
        private Slider _offsetY;
        private Text _status;

        public static void Open(ShipEditorWindow owner, bool sticker)
        {
            if (owner == null) return;
            var canvas = owner.GetComponentInParent<Canvas>() ??
                         owner.transform.root.GetComponentInChildren<Canvas>(true);
            if (canvas == null) return;

            var panelObject = new GameObject("ShipTextureCustomization", typeof(RectTransform));
            panelObject.transform.SetParent(canvas.transform, false);
            panelObject.transform.SetAsLastSibling();
            var panel = panelObject.AddComponent<ShipTextureCustomizationPanel>();
            var panelCanvas = panelObject.AddComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = 500;
            panelObject.AddComponent<GraphicRaycaster>();
            panel.Initialize(owner, sticker);
        }

        private void Initialize(ShipEditorWindow owner, bool sticker)
        {
            _owner = owner;
            _sticker = sticker;
            _baseSprite = owner.CurrentShipSprite ?? owner.OriginalShipSprite;

            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = gameObject.AddComponent<Image>();
            background.color = new Color(0.015f, 0.035f, 0.07f, 0.97f);

            var title = CreateText(_sticker ? "贴纸编辑" : "涂装编辑", 30);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-240, -70), new Vector2(240, -20));

            _previewImage = gameObject.AddComponent<RawImage>();
            _previewImage.color = Color.white;
            SetRect(_previewImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-260, -180), new Vector2(260, 180));

            var scaleLabel = CreateText("缩放", 20);
            SetRect(scaleLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-250, -250), new Vector2(-150, -215));
            _scale = CreateSlider(new Vector2(-140, -250), new Vector2(250, -215), 0.25f, 3f, 1f);
            _scale.onValueChanged.AddListener(_ => RefreshPreview());

            var xLabel = CreateText("水平", 18);
            SetRect(xLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-250, -305), new Vector2(-150, -275));
            _offsetX = CreateSlider(new Vector2(-140, -305), new Vector2(250, -275), -1f, 1f, 0f);
            _offsetX.onValueChanged.AddListener(_ => RefreshPreview());

            var yLabel = CreateText("垂直", 18);
            SetRect(yLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-250, -355), new Vector2(-150, -325));
            _offsetY = CreateSlider(new Vector2(-140, -355), new Vector2(250, -325), -1f, 1f, 0f);
            _offsetY.onValueChanged.AddListener(_ => RefreshPreview());

            CreateButton("选择图片", new Vector2(-460, 190), new Vector2(-220, 245), SelectImage);
            CreateButton("应用", new Vector2(-130, 190), new Vector2(130, 245), Apply);
            CreateButton("还原原图", new Vector2(220, 190), new Vector2(460, 245), Restore);
            CreateButton("关闭", new Vector2(220, -390), new Vector2(460, -335), Close);

            _status = CreateText("请选择一张图片", 18);
            SetRect(_status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-460, 20), new Vector2(460, 55));
            RefreshPreview();
        }

        private void SelectImage()
        {
            NativeFilePicker.RequestPermissionAsync(permission =>
            {
                if (permission != NativeFilePicker.Permission.Granted)
                {
                    SetStatus("未获得存储读取权限");
                    return;
                }

                NativeFilePicker.PickFile(path =>
                {
                    if (string.IsNullOrWhiteSpace(path)) return;
                    try
                    {
                        var bytes = File.ReadAllBytes(path);
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!texture.LoadImage(bytes, true))
                        {
                            Destroy(texture);
                            SetStatus("无法读取图片");
                            return;
                        }

                        if (_overlay != null) Destroy(_overlay);
                        _overlay = texture;
                        SetStatus("已载入：" + Path.GetFileName(path));
                        RefreshPreview();
                    }
                    catch (Exception error)
                    {
                        SetStatus("导入失败：" + error.Message);
                    }
                }, "*/*");
            }, true);
        }

        private void Apply()
        {
            if (_overlay == null)
            {
                SetStatus("请先选择图片");
                return;
            }

            if (PlayerShipTextureOverrides.Apply(_owner.CurrentShipId, _baseSprite, _overlay,
                    _sticker, _scale.value, new Vector2(_offsetX.value, _offsetY.value), out var error))
            {
                _owner.RefreshShipArtwork();
                SetStatus("已保存，原始贴图仍保留");
            }
            else
                SetStatus("保存失败：" + error);
        }

        private void Restore()
        {
            PlayerShipTextureOverrides.Restore(_owner.CurrentShipId);
            _owner.RefreshShipArtwork();
            SetStatus("已还原原始贴图");
            RefreshPreview();
        }

        private void Close()
        {
            if (_overlay != null) Destroy(_overlay);
            if (_preview != null) Destroy(_preview);
            Destroy(gameObject);
        }

        private void RefreshPreview()
        {
            if (_preview != null) Destroy(_preview);
            _preview = _overlay == null
                ? PlayerShipTextureOverrides.CreateBasePreview(_baseSprite)
                : PlayerShipTextureOverrides.CreatePreview(_baseSprite, _overlay, _sticker,
                    _scale.value, new Vector2(_offsetX.value, _offsetY.value));
            _previewImage.texture = _preview;
            if (_preview != null)
                _previewImage.uvRect = new Rect(0, 0, 1, 1);
        }

        private void SetStatus(string value)
        {
            if (_status != null) _status.text = value;
        }

        private Text CreateText(string value, int size)
        {
            var objectValue = new GameObject("Text", typeof(RectTransform), typeof(Text));
            objectValue.transform.SetParent(transform, false);
            var text = objectValue.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private Button CreateButton(string value, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var objectValue = new GameObject(value, typeof(RectTransform), typeof(Image), typeof(Button));
            objectValue.transform.SetParent(transform, false);
            var button = objectValue.GetComponent<Button>();
            objectValue.GetComponent<Image>().color = new Color(0.05f, 0.25f, 0.4f, 1f);
            button.onClick.AddListener(action);
            var label = CreateText(value, 20);
            label.transform.SetParent(objectValue.transform, false);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetRect((RectTransform)objectValue.transform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), min, max);
            return button;
        }

        private Slider CreateSlider(Vector2 min, Vector2 max, float minValue, float maxValue, float value)
        {
            var objectValue = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            objectValue.transform.SetParent(transform, false);
            var slider = objectValue.GetComponent<Slider>();
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.value = value;
            slider.targetGraphic = CreateImage(objectValue.transform, new Color(0.2f, 0.7f, 0.9f, 1f));
            SetRect((RectTransform)objectValue.transform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), min, max);
            return slider;
        }

        private Image CreateImage(Transform parent, Color color)
        {
            var imageObject = new GameObject("Graphic", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            SetRect((RectTransform)imageObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return image;
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
