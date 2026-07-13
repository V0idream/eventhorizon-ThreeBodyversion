using UnityEngine;
using UnityEngine.UI;
using Constructor;
using Economy;
using Services.Localization;
using Zenject;
using CommonComponents;
using ShipEditor.Model;
using UnityEngine.Events;
using Services.Gui;
using Gui.Utils;
using GameDatabase;
using GameDatabase.DataModel;

namespace ShipEditor.UI
{
	public class ComponentPanel : MonoBehaviour
	{
	    [Inject] private readonly ILocalization _localization;
		[Inject] private readonly IShipEditorModel _shipEditor;
		[Inject] private readonly IGuiManager _guiManager;
		[Inject] private readonly CommandList _commandList;
		[Inject] private readonly IDatabase _database;

		[SerializeField] private ComponentItem _componentItem;
		[SerializeField] private ControlsPanel _controlsPanel;
		[SerializeField] private DragHandler _dragHandler;
		[SerializeField] private DraggableComponent _draggableComponent;
		[SerializeField] private ComponentActionPanel _actionPanel;

		[SerializeField] private UnityEvent _closeRequested;

		private IComponentModel _componentModel;
		private ComponentInfo _componentInfo;
		private Button _creativeWorkshopButton;
		private Text _creativeWorkshopLabel;
		private GameObject _creativeWorkshopModal;

		private void OnEnable()
		{
			_shipEditor.Events.ComponentAdded += OnComponentAdded;
			_shipEditor.Events.ComponentRemoved += OnComponentRemoved;
			_shipEditor.Events.ComponentModified += OnComponentModified;
		}

		private void OnDisable()
		{
			_shipEditor.Events.ComponentAdded -= OnComponentAdded;
			_shipEditor.Events.ComponentRemoved -= OnComponentRemoved;
			_shipEditor.Events.ComponentModified -= OnComponentModified;
		}

		public bool Visible
		{
			get => gameObject.activeSelf;
			set => gameObject.SetActive(value);
		}

		public void OnKeyBindingChanged()
		{
			if (_componentModel != null)
			{
				_shipEditor.SetComponentKeyBinding(_componentModel, _controlsPanel.KeyBinding);
				_shipEditor.SetComponentBehaviour(_componentModel, _controlsPanel.ComponentMode);
			}
		}

		public void OnDragStarted(UnityEngine.EventSystems.PointerEventData eventData)
		{
			var keyBinding = _componentModel != null ? _componentModel.KeyBinding : _controlsPanel.KeyBinding;
			var behaviour = _componentModel != null ? _componentModel.Behaviour : _controlsPanel.ComponentMode;
			var persistedBarrelId = _componentModel?.PersistedBarrelId ?? int.MinValue;
			var content = new DraggableComponent.Content(_componentInfo, keyBinding, behaviour, persistedBarrelId);
			_draggableComponent.Initialize(content, eventData);
		}

		public void RemoveComponent()
		{
			_commandList.TryExecute(new RemoveComponentCommand(_shipEditor, _componentModel));
		}

		public void UnlockComponent()
		{
			_guiManager.ShowBuyConfirmationDialog(_localization.GetString("$UnlockConfirmation"), 
				_shipEditor.Inventory.GetUnlockPrice(_componentInfo), () => _shipEditor.UnlockComponent(_componentModel));
		}

		public void UnlockAllComponents()
		{
			Money totalPrice = 0;

			foreach (var item in _shipEditor.InstalledComponents)
				if (_shipEditor.CanBeUnlocked(item))
					totalPrice += _shipEditor.Inventory.GetUnlockPrice(item.Info).Amount;

			var price = Price.Common(totalPrice);
			_guiManager.ShowBuyConfirmationDialog(_localization.GetString("$UnlockAllConfirmation"), price, UnlockAllComponentsInternal);
		}

		public void SetInstalledComponent(IComponentModel model)
		{
			_componentModel = model;
			_componentInfo = model.Info;
			_componentItem.Initialize(model.Info);

			var component = _componentInfo.Data;
			_controlsPanel.Initialize(component, model.KeyBinding, _shipEditor.CompatibilityChecker.GetDefaultKey(component), model.Behaviour);
			UpdateCreativeWorkshopSelector(model);

            var canInstall = _shipEditor.CompatibilityChecker.IsCompatible(component) && _shipEditor.Inventory.GetQuantity(_componentInfo) > 0;
            _dragHandler.gameObject.SetActive(canInstall);

			if (!model.Locked)
				_actionPanel.Show(ComponentActionPanel.Status.CanRemove);
			else if (_shipEditor.CanBeUnlocked(_componentModel))
				_actionPanel.Show(ComponentActionPanel.Status.Locked);
			else
				_actionPanel.Show(ComponentActionPanel.Status.None);
		}

		public void SetInventoryComponent(ComponentInfo info)
		{
			_componentModel = null;
			_componentInfo = info;

			_componentItem.Initialize(info);

			var component = _componentInfo.Data;
			_controlsPanel.Initialize(component, -1, _shipEditor.CompatibilityChecker.GetDefaultKey(component), 0);
			HideCreativeWorkshopSelector();

			var canInstall = _shipEditor.CompatibilityChecker.IsCompatible(component);
			var alreadyInstalled = !canInstall && _shipEditor.CompatibilityChecker.ComponentLimitReached(component);

			_dragHandler.gameObject.SetActive(canInstall);

			if (canInstall)
				_actionPanel.Show(ComponentActionPanel.Status.CanInstall);
			else if (alreadyInstalled)
				_actionPanel.Show(ComponentActionPanel.Status.AlreadyInstalled);
			else
				_actionPanel.Show(ComponentActionPanel.Status.NotCompatible);
		}

		private void UnlockAllComponentsInternal()
		{
			foreach (var item in _shipEditor.InstalledComponents)
				if (_shipEditor.CanBeUnlocked(item))
					_shipEditor.UnlockComponent(item);
		}

		private void OnComponentAdded(IComponentModel model)
		{
			if (_shipEditor.Inventory.GetQuantity(_componentInfo) == 0)
				_closeRequested?.Invoke();
		}

		private void OnComponentRemoved(IComponentModel model)
		{
			HideCreativeWorkshopSelector();
			_closeRequested?.Invoke();
		}

		private void OnComponentModified(IComponentModel model)
		{
			if (model == _componentModel)
				SetInstalledComponent(model);
		}

		private void UpdateCreativeWorkshopSelector(IComponentModel model)
		{
			if (model == null || model.Data.Id.Value != ThreeBodyContentRules.CreativeWorkshopComponentId)
			{
				HideCreativeWorkshopSelector();
				return;
			}

			EnsureCreativeWorkshopSelector();
			_creativeWorkshopButton.gameObject.SetActive(true);
			if (ThreeBodyContentRules.TryGetCreativeWorkshopDrone(_database, model.PersistedBarrelId, model.Behaviour, out var build))
				_creativeWorkshopLabel.text = "无人机配置：" + _localization.GetString(build.Ship.Name) + " #" + build.Id.Value;
			else
				_creativeWorkshopLabel.text = "无人机配置：默认（观众）";
		}

		private void HideCreativeWorkshopSelector()
		{
			if (_creativeWorkshopButton != null)
				_creativeWorkshopButton.gameObject.SetActive(false);
			if (_creativeWorkshopModal != null)
				_creativeWorkshopModal.SetActive(false);
		}

		private void EnsureCreativeWorkshopSelector()
		{
			if (_creativeWorkshopButton != null)
				return;

			var buttonObject = new GameObject("CreativeWorkshopSelector", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
			buttonObject.layer = gameObject.layer;
			buttonObject.transform.SetParent(transform, false);
			var rect = buttonObject.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.08f, 0.03f);
			rect.anchorMax = new Vector2(0.92f, 0.13f);
			rect.offsetMin = rect.offsetMax = Vector2.zero;
			buttonObject.GetComponent<Image>().color = new Color(0.07f, 0.34f, 0.46f, 0.96f);
			_creativeWorkshopButton = buttonObject.GetComponent<Button>();
			_creativeWorkshopButton.onClick.AddListener(OpenCreativeWorkshopSelector);

			var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
			labelObject.layer = buttonObject.layer;
			labelObject.transform.SetParent(buttonObject.transform, false);
			_creativeWorkshopLabel = labelObject.GetComponent<Text>();
			_creativeWorkshopLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			_creativeWorkshopLabel.fontSize = 21;
			_creativeWorkshopLabel.alignment = TextAnchor.MiddleCenter;
			_creativeWorkshopLabel.color = Color.white;
			var labelRect = _creativeWorkshopLabel.rectTransform;
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
		}

		private void OpenCreativeWorkshopSelector()
		{
			if (_componentModel == null || _componentModel.Data.Id.Value != ThreeBodyContentRules.CreativeWorkshopComponentId)
				return;

			if (_creativeWorkshopModal != null)
			{
				_creativeWorkshopModal.SetActive(true);
				return;
			}

			var canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
			if (canvas == null)
				return;

			_creativeWorkshopModal = new GameObject("CreativeWorkshopBuildSelector", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(Image), typeof(GraphicRaycaster));
			_creativeWorkshopModal.layer = canvas.gameObject.layer;
			var root = _creativeWorkshopModal.GetComponent<RectTransform>();
			root.SetParent(canvas.transform, false);
			root.anchorMin = new Vector2(0.06f, 0.05f);
			root.anchorMax = new Vector2(0.94f, 0.95f);
			root.offsetMin = root.offsetMax = Vector2.zero;
			_creativeWorkshopModal.GetComponent<Image>().color = new Color(0.01f, 0.06f, 0.1f, 0.99f);
			var overlay = _creativeWorkshopModal.GetComponent<Canvas>();
			overlay.overrideSorting = true;
			overlay.sortingOrder = canvas.sortingOrder + 200;

			var title = CreateSelectorText(root, "Title", "创意工坊：选择无人机配置", 28, TextAnchor.MiddleCenter);
			title.rectTransform.anchorMin = new Vector2(0.02f, 0.9f);
			title.rectTransform.anchorMax = new Vector2(0.98f, 0.99f);
			title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

			var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(ScrollRect));
			viewportObject.layer = _creativeWorkshopModal.layer;
			var viewport = viewportObject.GetComponent<RectTransform>();
			viewport.SetParent(root, false);
			viewport.anchorMin = new Vector2(0.03f, 0.13f);
			viewport.anchorMax = new Vector2(0.97f, 0.88f);
			viewport.offsetMin = viewport.offsetMax = Vector2.zero;
			viewportObject.GetComponent<Image>().color = new Color(0.02f, 0.12f, 0.17f, 0.98f);
			viewportObject.GetComponent<Mask>().showMaskGraphic = true;

			var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
			contentObject.layer = _creativeWorkshopModal.layer;
			var content = contentObject.GetComponent<RectTransform>();
			content.SetParent(viewport, false);
			content.anchorMin = new Vector2(0f, 1f);
			content.anchorMax = Vector2.one;
			content.pivot = new Vector2(0.5f, 1f);
			content.offsetMin = content.offsetMax = Vector2.zero;
			var group = contentObject.GetComponent<VerticalLayoutGroup>();
			group.spacing = 5f;
			group.padding = new RectOffset(8, 8, 8, 8);
			group.childForceExpandHeight = false;
			contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			var scroll = viewportObject.GetComponent<ScrollRect>();
			scroll.viewport = viewport;
			scroll.content = content;
			scroll.horizontal = false;
			scroll.vertical = true;

			foreach (var build in ThreeBodyContentRules.GetCreativeWorkshopBuilds(_database))
				CreateCreativeWorkshopBuildRow(content, build);

			var close = CreateSelectorButton(root, "Close", "关闭", new Vector2(0.33f, 0.025f), new Vector2(0.67f, 0.105f));
			close.onClick.AddListener(() => _creativeWorkshopModal.SetActive(false));
		}

		private void CreateCreativeWorkshopBuildRow(RectTransform parent, ShipBuild build)
		{
			var rowObject = new GameObject("Build_" + build.Id.Value, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
			rowObject.layer = parent.gameObject.layer;
			rowObject.transform.SetParent(parent, false);
			rowObject.GetComponent<Image>().color = new Color(0.03f, 0.22f, 0.3f, 0.96f);
			rowObject.GetComponent<LayoutElement>().preferredHeight = 58f;
			var text = CreateSelectorText(rowObject.transform, "Label", _localization.GetString(build.Ship.Name) + "  ·  配置 #" + build.Id.Value, 20, TextAnchor.MiddleLeft);
			text.rectTransform.anchorMin = new Vector2(0.04f, 0f);
			text.rectTransform.anchorMax = new Vector2(0.96f, 1f);
			text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
			rowObject.GetComponent<Button>().onClick.AddListener(() => SelectCreativeWorkshopBuild(build));
		}

		private void SelectCreativeWorkshopBuild(ShipBuild build)
		{
			if (_componentModel == null || !ThreeBodyContentRules.TryEncodeCreativeWorkshopDrone(_database, build, out var persistedBarrelId, out var behaviour))
				return;

			_shipEditor.SetComponentPersistedBarrelId(_componentModel, persistedBarrelId);
			_shipEditor.SetComponentBehaviour(_componentModel, behaviour);
			if (_creativeWorkshopModal != null)
				_creativeWorkshopModal.SetActive(false);
			UpdateCreativeWorkshopSelector(_componentModel);
		}

		private static Text CreateSelectorText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
		{
			var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
			gameObject.layer = parent.gameObject.layer;
			gameObject.transform.SetParent(parent, false);
			var text = gameObject.GetComponent<Text>();
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			text.text = value;
			text.fontSize = fontSize;
			text.alignment = alignment;
			text.color = Color.white;
			return text;
		}

		private static Button CreateSelectorButton(RectTransform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax)
		{
			var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
			gameObject.layer = parent.gameObject.layer;
			var rect = gameObject.GetComponent<RectTransform>();
			rect.SetParent(parent, false);
			rect.anchorMin = anchorMin;
			rect.anchorMax = anchorMax;
			rect.offsetMin = rect.offsetMax = Vector2.zero;
			gameObject.GetComponent<Image>().color = new Color(0.05f, 0.36f, 0.5f, 1f);
			var text = CreateSelectorText(rect, "Label", label, 22, TextAnchor.MiddleCenter);
			text.rectTransform.anchorMin = Vector2.zero;
			text.rectTransform.anchorMax = Vector2.one;
			text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
			return gameObject.GetComponent<Button>();
		}
	}
}
