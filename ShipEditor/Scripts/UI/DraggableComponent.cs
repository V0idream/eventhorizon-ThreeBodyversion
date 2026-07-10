using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Zenject;
using Services.Resources;
using Constructor;
using GameDatabase.Model;

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

		private RectTransform RectTransform
		{
			get
			{
				if (_rectTransform == null) 
					_rectTransform = GetComponent<RectTransform>();

				return _rectTransform;
			}
		}

		public void Initialize(Content content, PointerEventData eventData)
		{
			_content = content;
			var blockSize = _helper.GetCellSize();
			GetOccupiedBounds(content.Layout.Data, content.Layout.Size,
				out var minX, out var minY, out var width, out var height);

			gameObject.SetActive(true);
            var size = new Vector2(width * blockSize.x, height * blockSize.y);
            SetScreenPosition(eventData);
            RectTransform.localEulerAngles = new Vector3(0, 0, _helper.GetShipRotation());
            RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            _icon.SetIconFitted(_resourceLocator.GetSprite(content.Icon), content.Color);

            eventData.pointerDrag = gameObject;
            ExecuteEvents.Execute<IBeginDragHandler>(gameObject, eventData, ExecuteEvents.beginDragHandler);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            SetScreenPosition(eventData);
			// The editor grid already converts the pointer position using the full
			// component layout. Applying an occupied-bounds offset a second time made
			// the installed cell differ from the cell under the finger.
			_dragging?.Invoke(_content, _helper.ScreenToWorld(eventData.position));
        }

        public void OnEndDrag(PointerEventData eventData)
		{
			gameObject.SetActive(false);
			_dropped?.Invoke(_content, _helper.ScreenToWorld(eventData.position));
		}

		private void SetScreenPosition(PointerEventData eventData)
		{
			if (RectTransform.parent is RectTransform parent &&
				RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position,
					eventData.pressEventCamera, out var localPosition))
				RectTransform.anchoredPosition = localPosition;
			else
				RectTransform.position = eventData.position;
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
