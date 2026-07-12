using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Zenject;
using Services.Resources;
using Constructor;
using GameDatabase.Model;
using GameDatabase.Enums;

namespace ShipEditor.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class DraggableComponent : MonoBehaviour, IDragHandler, IPointerDownHandler, IBeginDragHandler, IEndDragHandler
    {
        [Inject] private readonly IResourceLocator _resourceLocator;
        [SerializeField] private ComponentImage _icon;
        [SerializeField] private CanvasTransformHelper _helper;
        [SerializeField] private UnityEvent<Content, Vector2> _dropped;
        [SerializeField] private UnityEvent<Content, Vector2> _dragging;
        private RectTransform _rectTransform;
        private Content _content;
        private Vector2 _dropWorldOffset;

        private RectTransform RectTransform => _rectTransform ??= GetComponent<RectTransform>();

        public void Initialize(Content content, PointerEventData eventData)
        {
            _content = content;
            var blockSize = _helper.GetCellSize();
            GetOccupiedBounds(content.Layout.Data, content.Layout.Size,
                out var minX, out var minY, out var width, out var height);
            gameObject.SetActive(true);
            var size = new Vector2(width * blockSize.x, height * blockSize.y);
            RectTransform.position = eventData.position;
            RectTransform.localEulerAngles = new Vector3(0, 0, _helper.GetShipRotation());
            RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            _icon.SetIconFitted(_resourceLocator.GetSprite(content.Icon), content.Color,
                content.Component.Data.ContentSource == ContentSource.ThreeBody);

            // The finger follows the centre of the visible (occupied) cells, while
            // WorldToCell works from the centre of the component's square layout.
            // Translate between those two centres once, in ship-local coordinates,
            // then rotate into world space.  Using the camera-relative angle here
            // was the reason database/preset editing selected a different cell.
            var layoutCenter = Vector2.one * (content.Layout.Size * 0.5f);
            var occupiedCenter = new Vector2(minX + width * 0.5f, minY + height * 0.5f);
            var delta = layoutCenter - occupiedCenter;
            var localWorldOffset = new Vector2(delta.x, -delta.y) * _helper.GetWorldCellSize();
            _dropWorldOffset = RotationHelpers.Transform(localWorldOffset, _helper.GetShipWorldRotation());
            eventData.pointerDrag = gameObject;
            ExecuteEvents.Execute<IBeginDragHandler>(gameObject, eventData, ExecuteEvents.beginDragHandler);
        }

        public void OnPointerDown(PointerEventData eventData) { }
        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransform.position = eventData.position;
            _dragging?.Invoke(_content, (Vector2)_helper.ScreenToWorld(eventData.position) + _dropWorldOffset);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            gameObject.SetActive(false);
            _dropped?.Invoke(_content, (Vector2)_helper.ScreenToWorld(eventData.position) + _dropWorldOffset);
        }

        private static void GetOccupiedBounds(string data, int size, out int minX, out int minY,
            out int width, out int height)
        {
            minX = size;
            minY = size;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                if ((GameDatabase.Enums.CellType)data[y * size + x] == GameDatabase.Enums.CellType.Empty)
                    continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }

            if (maxX < minX || maxY < minY)
            {
                minX = minY = 0;
                width = height = 1;
                return;
            }

            width = maxX - minX + 1;
            height = maxY - minY + 1;
        }

        public readonly struct Content
        {
            public readonly ComponentInfo Component;
            public readonly int KeyBinding;
            public readonly int Behaviour;
            public Layout Layout => Component.Data.Layout;
            public SpriteId Icon => Component.Data.Icon;
            public Color Color => Component.Data.Color;
            public Content(ComponentInfo component, int keyBinding = 0, int behaviour = 0)
            {
                Component = component;
                KeyBinding = keyBinding;
                Behaviour = behaviour;
            }
        }
    }
}
