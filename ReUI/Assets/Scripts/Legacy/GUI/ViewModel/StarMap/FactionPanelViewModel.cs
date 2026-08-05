using System.Collections.Generic;
using System.Linq;
using Domain.Quests;
using Economy.ItemType;
using Economy.Products;
using Galaxy;
using GameDatabase.DataModel;
using UnityEngine;
using UnityEngine.UI;
using GameModel.Quests;
using GameServices.Player;
using GameServices.Quests;
using GameStateMachine.States;
using Services.Localization;
using Services.Messenger;
using Session;
using Zenject;
using Galaxy.StarContent;

namespace ViewModel
{
	public class FactionPanelViewModel : MonoBehaviour
	{
        [Inject] private readonly ItemTypeFactory _factory;
        [Inject] private readonly IMessenger _messenger;
	    [Inject] private readonly MotherShip _motherShip;
	    [Inject] private readonly IQuestManager _questManager;
	    [Inject] private readonly OpenShopSignal.Trigger _openShopTrigger;
	    [Inject] private readonly OpenWorkshopSignal.Trigger _openWorkshopTrigger;
	    [Inject] private readonly OpenShipyardSignal.Trigger _openShipyardTrigger;
	    [Inject] private readonly InventoryFactory _inventoryFactory;
	    [Inject] private readonly ISessionData _session;
		[Inject] private readonly GameModel.RegionMap _regionMap;
	    [Inject] private readonly ILocalization _localization;
        [Inject] private readonly QuestEventSignal.Trigger _questEventTrigger;
        [Inject] private readonly StartBattleSignal.Trigger _startBattleTrigger;
		[Inject] private readonly StarContentChangedSignal.Trigger _starContentChangedTrigger;

		[SerializeField] private GameObject CaptureButton;
	    [SerializeField] private GameObject CaptureDescription;
	    [SerializeField] private GameObject MilitaryPowerPanel;
	    [SerializeField] private GameObject ReputationPanel;
        [SerializeField] private GameObject ShopButton;
	    [SerializeField] private GameObject CraftButton;
	    [SerializeField] private GameObject ShipyardButton;
	    [SerializeField] private GameObject MissionButton;
	    [SerializeField] private Text FactionName;
	    [SerializeField] private Text PowerText;
	    [SerializeField] private Text ReputationText;

        public void OpenStore()
		{
            _openShopTrigger.Fire(_inventoryFactory.CreateFactionInventory(_motherShip.CurrentStar.Region), _inventoryFactory.CreatePlayerInventory());
        }

		public void CaptureBase()
		{
			UnityEngine.Debug.Log("FactionPanelViewModel.CaptureBase");

			_motherShip.CurrentStar.CaptureBase();
		}

        public void PeacefullyTransferBase()
        {
            if (_motherShip.CurrentStar.PeacefulTransferBase())
                OnEnable();
        }

        public void DefendBase()
        {
            _motherShip.CurrentStar.DefendBase();
        }

        public static bool IncludeStarshipEarthAllies { get; private set; }

        public bool MissionsAvailable
        {
            get
            {
                if (_motherShip.CurrentStar.Region.Faction.NoMissions) return false;
                return !_questManager.Quests.Any(item => item.IsFactionMission(_motherShip.CurrentStar.Id));
            }
        }

        public void TakeMission()
	    {
            MissionButton.gameObject.SetActive(false);
	        _questEventTrigger.Fire(new StarEventData(QuestEventType.FactionMissionAccepted, _motherShip.CurrentStar.Region.HomeStar));
        }

		private void OnEnable()
		{
            ConfigurePreview4Layout();
			BindFacilityButtons();
			var region = _motherShip.CurrentStar.Region;

		    FactionName.text = _localization.GetString(region.Faction.Name);
            FactionName.color = region.Faction.Color;

		    var reputation = _session.Quests.GetFactionRelations(region.HomeStar);

            if (region.IsCaptured)
		    {
                SetJointControlsVisible(false);
		        SetPeacefulTransferVisible(false);
                SetDefenseVisible(true);
				SetFacilityTypeVisible(true);
				RefreshFacilityTypeButton(region);
		        CaptureButton.gameObject.SetActive(false);
		        CaptureDescription.gameObject.SetActive(false);
		        MilitaryPowerPanel.gameObject.SetActive(false);
		        ReputationPanel.gameObject.SetActive(false);
		        ShopButton.SetActive(true);
		        CraftButton.SetActive(true);
		        ShipyardButton.SetActive(true);
		        MissionButton.gameObject.SetActive(false);
                return;
		    }

            CaptureButton.gameObject.SetActive(true);
            SetDefenseVisible(false);
			SetFacilityTypeVisible(false);
            SetJointControlsVisible(true);
		    CaptureDescription.gameObject.SetActive(true);
		    MilitaryPowerPanel.gameObject.SetActive(true);
		    ReputationPanel.gameObject.SetActive(region.Faction != Faction.Empty);
            var relationState = reputation > 25 ? "友好" : reputation < -25 ? "敌对" : "中立";
		    ReputationText.text = $"{reputation:+0;-0;0}  {relationState}";
		    SetPeacefulTransferVisible(reputation > 25);
		    PowerText.text = region.BaseDefensePower + "%";

            MissionButton.gameObject.SetActive(MissionsAvailable);

			ShopButton.SetActive(reputation >= 5);
			CraftButton.SetActive(reputation >= 60);
            ShipyardButton.SetActive(reputation >= 90);
		}

		private bool _facilityButtonsBound;

		private void BindFacilityButtons()
		{
			if (_facilityButtonsBound)
				return;

			var workshopButton = CraftButton != null ? CraftButton.GetComponent<Button>() : null;
			var shipyardButton = ShipyardButton != null ? ShipyardButton.GetComponent<Button>() : null;
			if (workshopButton != null)
				workshopButton.onClick.AddListener(OpenWorkshop);
			if (shipyardButton != null)
				shipyardButton.onClick.AddListener(OpenShipyard);
			_facilityButtonsBound = workshopButton != null || shipyardButton != null;
		}

		private void OpenWorkshop()
		{
			var region = _motherShip.CurrentStar.Region;
			_openWorkshopTrigger.Fire(region.Faction, Mathf.Max(1, region.HomeStarLevel));
		}

		private void OpenShipyard()
		{
			var region = _motherShip.CurrentStar.Region;
			_openShipyardTrigger.Fire(region.Faction, Mathf.Max(1, region.HomeStarLevel));
		}

        private void ConfigurePreview4Layout()
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
            {
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 14;
                text.resizeTextMaxSize = Mathf.Min(text.fontSize, 26);
            }

            var captureRect = CaptureButton.GetComponent<RectTransform>();
            if (captureRect == null)
                return;

            var buttonsRect = CaptureButton.transform.parent.GetComponent<RectTransform>();
            if (buttonsRect != null)
                buttonsRect.sizeDelta = new Vector2(Mathf.Max(430f, buttonsRect.sizeDelta.x), Mathf.Max(560f, buttonsRect.sizeDelta.y));
            var captureLayout = CaptureButton.GetComponent<LayoutElement>() ?? CaptureButton.AddComponent<LayoutElement>();
            captureLayout.minWidth = 410f;
            captureLayout.preferredWidth = 410f;
            captureLayout.minHeight = 78f;
            captureLayout.preferredHeight = 78f;
            captureLayout.flexibleWidth = 0f;
            captureRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 410f);
            captureRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 78f);
            foreach (var captureLabel in CaptureButton.GetComponentsInChildren<Text>(true))
            {
                captureLabel.resizeTextForBestFit = true;
                captureLabel.resizeTextMinSize = 10;
                captureLabel.resizeTextMaxSize = Mathf.Max(18, captureLabel.fontSize);
                captureLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            var emblem = transform.Find("Body/Left/Faction") ?? transform.Find("Faction");
            if (emblem != null)
                emblem.localScale = Vector3.one * 0.72f;

            var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            _alliedAttackPanel = (rootCanvas != null ? rootCanvas.GetComponentsInChildren<Transform>(true) : GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "Preview7AlliedAttackDialog")?.gameObject;
            _jointAttackButton = GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "Preview5JointAttackButton")?.gameObject;
			EnsurePeacefulTransferButton();
			EnsureDefenseButton();
			EnsureFacilityTypeControls();
            if (_alliedAttackPanel != null && _jointAttackButton != null)
                return;

            var panel = new GameObject("Preview7AlliedAttackDialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = panel.GetComponent<RectTransform>();
            rect.SetParent(rootCanvas != null ? rootCanvas.transform : transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0.02f, 0.05f, 0.78f);
            panel.SetActive(false);
            _alliedAttackPanel = panel;

            var dismissButton = panel.AddComponent<Button>();
            dismissButton.targetGraphic = panel.GetComponent<Image>();
            dismissButton.onClick.AddListener(() => panel.SetActive(false));

            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.SetParent(rect, false);
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(540f, 300f);
            card.GetComponent<Image>().color = new Color(0.015f, 0.09f, 0.15f, 0.98f);
            var cardOutline = card.GetComponent<Outline>();
            cardOutline.effectColor = new Color(0.12f, 0.72f, 1f, 0.95f);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            var title = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.SetParent(cardRect, false);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -18f);
            titleRect.sizeDelta = new Vector2(-36f, 56f);
            var titleText = title.GetComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 28;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.5f, 0.9f, 1f);
            titleText.text = "选择联合进攻舰队";

            var toggleObject = new GameObject("StarshipEarth", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
            var toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.SetParent(cardRect, false);
            toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
            toggleRect.pivot = new Vector2(0.5f, 0.5f);
            toggleRect.anchoredPosition = new Vector2(0f, 20f);
            toggleRect.sizeDelta = new Vector2(460f, 72f);
            toggleObject.GetComponent<Image>().color = new Color(0.035f, 0.18f, 0.25f, 1f);

            var checkBackground = new GameObject("CheckBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var checkBackgroundRect = checkBackground.GetComponent<RectTransform>();
            checkBackgroundRect.SetParent(toggleRect, false);
            checkBackgroundRect.anchorMin = checkBackgroundRect.anchorMax = new Vector2(0f, 0.5f);
            checkBackgroundRect.pivot = new Vector2(0f, 0.5f);
            checkBackgroundRect.sizeDelta = new Vector2(38f, 38f);
            checkBackgroundRect.anchoredPosition = new Vector2(18f, 0f);
            checkBackground.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.08f, 1f);

            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var checkRect = check.GetComponent<RectTransform>();
            checkRect.SetParent(checkBackgroundRect, false);
            checkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            check.GetComponent<Image>().color = new Color(0.12f, 0.75f, 1f, 1f);

            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.SetParent(toggleRect, false);
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(72f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
            var labelText = label.GetComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 22;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = Color.white;
            labelText.text = "星舰地球支援舰队";

            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = toggleObject.GetComponent<Image>();
            toggle.isOn = IncludeStarshipEarthAllies;

            var confirmObject = new GameObject("Confirm", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            var confirmRect = confirmObject.GetComponent<RectTransform>();
            confirmRect.SetParent(cardRect, false);
            confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(0.5f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.anchoredPosition = new Vector2(0f, 22f);
            confirmRect.sizeDelta = new Vector2(300f, 62f);
            confirmObject.GetComponent<Image>().color = new Color(0.04f, 0.46f, 0.68f, 1f);
            confirmObject.GetComponent<Outline>().effectColor = new Color(0.3f, 0.9f, 1f, 0.8f);
            var confirmText = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var confirmTextRect = confirmText.GetComponent<RectTransform>();
            confirmTextRect.SetParent(confirmRect, false);
            confirmTextRect.anchorMin = Vector2.zero;
            confirmTextRect.anchorMax = Vector2.one;
            confirmTextRect.offsetMin = Vector2.zero;
            confirmTextRect.offsetMax = Vector2.zero;
            var confirmLabel = confirmText.GetComponent<Text>();
            confirmLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            confirmLabel.fontSize = 24;
            confirmLabel.alignment = TextAnchor.MiddleCenter;
            confirmLabel.color = Color.white;
            confirmLabel.text = "确认选择";
            confirmObject.GetComponent<Button>().onClick.AddListener(() =>
            {
                IncludeStarshipEarthAllies = toggle.isOn;
                panel.SetActive(false);
            });

            var jointObject = new GameObject("Preview5JointAttackButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var jointRect = jointObject.GetComponent<RectTransform>();
            jointRect.SetParent(CaptureButton.transform.parent, false);
            jointObject.transform.SetAsLastSibling();
            jointRect.sizeDelta = new Vector2(410f, 78f);
            var jointLayout = jointObject.AddComponent<LayoutElement>();
            jointLayout.minWidth = 410f;
            jointLayout.preferredWidth = 410f;
            jointLayout.minHeight = 78f;
            jointLayout.preferredHeight = 78f;
            jointLayout.flexibleWidth = 0f;
            var captureImage = CaptureButton.GetComponent<Image>();
            var jointImage = jointObject.GetComponent<Image>();
            jointImage.color = new Color(0.025f, 0.32f, 0.48f, 1f);
            var jointOutline = jointObject.AddComponent<Outline>();
            jointOutline.effectColor = new Color(0.2f, 0.8f, 1f, 0.85f);
            jointOutline.effectDistance = new Vector2(2f, -2f);
            jointObject.GetComponent<Button>().onClick.AddListener(() =>
            {
                toggle.isOn = IncludeStarshipEarthAllies;
                panel.SetActive(true);
                panel.transform.SetAsLastSibling();
            });

            var jointLabel = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var jointLabelRect = jointLabel.GetComponent<RectTransform>();
            jointLabelRect.SetParent(jointRect, false);
            jointLabelRect.anchorMin = Vector2.zero;
            jointLabelRect.anchorMax = Vector2.one;
            jointLabelRect.offsetMin = new Vector2(8f, 4f);
            jointLabelRect.offsetMax = new Vector2(-8f, -4f);
            var jointText = jointLabel.GetComponent<Text>();
            jointText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            jointText.fontSize = 22;
            jointText.resizeTextForBestFit = true;
            jointText.resizeTextMinSize = 14;
            jointText.alignment = TextAnchor.MiddleCenter;
            jointText.color = Color.white;
            jointText.text = "◇  联合进攻";
            _jointAttackButton = jointObject;
        }

        private void EnsurePeacefulTransferButton()
        {
            _peacefulTransferButton = GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "PeacefulTransferButton")?.gameObject;
            if (_peacefulTransferButton != null)
                return;

            var buttonObject = new GameObject("PeacefulTransferButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(CaptureButton.transform.parent, false);
            rect.sizeDelta = new Vector2(410f, 78f);
            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = 410f;
            layout.minHeight = layout.preferredHeight = 78f;
            layout.flexibleWidth = 0f;
            buttonObject.GetComponent<Image>().color = new Color(0.04f, 0.42f, 0.34f, 1f);
            var outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.35f, 1f, 0.72f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            buttonObject.GetComponent<Button>().onClick.AddListener(PeacefullyTransferBase);

            var textTemplate = CaptureButton.GetComponentInChildren<Text>(true);
            Text label;
            if (textTemplate != null)
            {
                label = Instantiate(textTemplate, rect);
                label.name = "Label";
            }
            else
            {
                label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
                label.transform.SetParent(rect, false);
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
            label.text = "和平交接";
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 24;
            label.color = Color.white;
            _peacefulTransferButton = buttonObject;
        }

        private void SetPeacefulTransferVisible(bool visible)
        {
            if (_peacefulTransferButton != null)
                _peacefulTransferButton.SetActive(visible);
        }

        private void EnsureDefenseButton()
        {
            _defenseButton = GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "StarbaseDefenseButton")?.gameObject;
            if (_defenseButton != null)
                return;

            var buttonObject = new GameObject("StarbaseDefenseButton", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(CaptureButton.transform.parent, false);
            rect.sizeDelta = new Vector2(410f, 78f);
            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = 410f;
            layout.minHeight = layout.preferredHeight = 78f;
            layout.flexibleWidth = 0f;
            buttonObject.GetComponent<Image>().color = new Color(0.05f, 0.31f, 0.58f, 1f);
            var outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.32f, 0.78f, 1f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            buttonObject.GetComponent<Button>().onClick.AddListener(DefendBase);

            var template = CaptureButton.GetComponentInChildren<Text>(true);
            var label = template != null
                ? Instantiate(template, rect)
                : new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            if (template == null)
            {
                label.transform.SetParent(rect, false);
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            label.name = "Label";
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 4f);
            label.rectTransform.offsetMax = new Vector2(-8f, -4f);
            label.text = "防卫";
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 26;
            label.color = Color.white;
            _defenseButton = buttonObject;
        }

        private void SetDefenseVisible(bool visible)
        {
            if (_defenseButton != null)
                _defenseButton.SetActive(visible);
        }

		private void EnsureFacilityTypeControls()
		{
			_facilityTypeButton = GetComponentsInChildren<Transform>(true)
				.FirstOrDefault(item => item.name == "StarbaseFacilityTypeButton")?.gameObject;
			if (_facilityTypeButton == null)
			{
				_facilityTypeButton = new GameObject("StarbaseFacilityTypeButton", typeof(RectTransform),
					typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
				var rect = _facilityTypeButton.GetComponent<RectTransform>();
				rect.SetParent(CaptureButton.transform.parent, false);
				rect.sizeDelta = new Vector2(410f, 92f);
				var layout = _facilityTypeButton.GetComponent<LayoutElement>();
				layout.minWidth = layout.preferredWidth = 410f;
				layout.minHeight = layout.preferredHeight = 92f;
				layout.flexibleWidth = 0f;
				_facilityTypeButton.GetComponent<Button>().onClick.AddListener(OpenFacilityTypePanel);

				_facilityTypeLabel = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
					typeof(Text)).GetComponent<Text>();
				_facilityTypeLabel.transform.SetParent(rect, false);
				_facilityTypeLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
				_facilityTypeLabel.fontSize = 21;
				_facilityTypeLabel.fontStyle = FontStyle.Bold;
				_facilityTypeLabel.alignment = TextAnchor.MiddleCenter;
				_facilityTypeLabel.resizeTextForBestFit = true;
				_facilityTypeLabel.resizeTextMinSize = 13;
				_facilityTypeLabel.resizeTextMaxSize = 22;
				_facilityTypeLabel.color = Color.white;
				_facilityTypeLabel.rectTransform.anchorMin = Vector2.zero;
				_facilityTypeLabel.rectTransform.anchorMax = Vector2.one;
				_facilityTypeLabel.rectTransform.offsetMin = new Vector2(10f, 5f);
				_facilityTypeLabel.rectTransform.offsetMax = new Vector2(-10f, -5f);
			}
			else
			{
				_facilityTypeLabel = _facilityTypeButton.GetComponentInChildren<Text>(true);
			}

			var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
			var searchRoot = rootCanvas != null ? rootCanvas.transform : transform.root;
			_facilityTypePanel = searchRoot.GetComponentsInChildren<Transform>(true)
				.FirstOrDefault(item => item.name == "StarbaseFacilityTypePanel")?.gameObject;
			if (_facilityTypePanel == null)
				CreateFacilityTypePanel(searchRoot);
			EnsureFacilityActionButton();
		}

		private void CreateFacilityTypePanel(Transform parent)
		{
			_facilityTypePanel = new GameObject("StarbaseFacilityTypePanel", typeof(RectTransform),
				typeof(CanvasRenderer), typeof(Image), typeof(Button));
			var panelRect = _facilityTypePanel.GetComponent<RectTransform>();
			panelRect.SetParent(parent, false);
			panelRect.anchorMin = Vector2.zero;
			panelRect.anchorMax = Vector2.one;
			panelRect.offsetMin = Vector2.zero;
			panelRect.offsetMax = Vector2.zero;
			_facilityTypePanel.GetComponent<Image>().color = new Color(0f, 0f, 0.03f, 0.82f);
			_facilityTypePanel.GetComponent<Button>().onClick.AddListener(() => _facilityTypePanel.SetActive(false));

			var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
				typeof(Outline));
			var cardRect = card.GetComponent<RectTransform>();
			cardRect.SetParent(panelRect, false);
			cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
			cardRect.pivot = new Vector2(0.5f, 0.5f);
			cardRect.sizeDelta = new Vector2(760f, 850f);
			card.GetComponent<Image>().color = new Color(0.035f, 0.015f, 0.075f, 0.98f);
			var cardOutline = card.GetComponent<Outline>();
			cardOutline.effectColor = new Color(0.62f, 0.32f, 1f, 0.95f);
			cardOutline.effectDistance = new Vector2(2f, -2f);

			var title = CreateFacilityText(cardRect, "Title", 30, FontStyle.Bold);
			title.text = "选择空间站类型";
			SetFacilityRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
				new Vector2(24f, -82f), new Vector2(-24f, -18f));

			_facilityTypeTierText = CreateFacilityText(cardRect, "Tier", 21, FontStyle.Normal);
			SetFacilityRect(_facilityTypeTierText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
				new Vector2(24f, -126f), new Vector2(-24f, -82f));

			CreateFacilityOption(cardRect, CapturedStarbaseFacilityType.Trade, 128f,
				"贸易站", "每阶每日奖励 +1000信用点、+1星币");
			CreateFacilityOption(cardRect, CapturedStarbaseFacilityType.Border, 266f,
				"边防站", "每阶使对应势力支援舰队等级 +1；十阶追加1艘战列舰");
			CreateFacilityOption(cardRect, CapturedStarbaseFacilityType.Research, 404f,
				"科研站", "每阶每日奖励 +1对应势力科研点");
			CreateFacilityOption(cardRect, CapturedStarbaseFacilityType.Prism, 542f,
				"棱镜", "十阶解锁。每日充能一次，可直接占领50光年内的一座空间站");
			CreateFacilityOption(cardRect, CapturedStarbaseFacilityType.Lane, 680f,
				"航道", "十阶解锁。可连接并传送至100光年内的另一座航道空间站");

			_facilityTypePanel.SetActive(false);
		}

		private void CreateFacilityOption(RectTransform parent, CapturedStarbaseFacilityType type,
			float top, string title, string description)
		{
			var option = new GameObject(type + "Option", typeof(RectTransform), typeof(CanvasRenderer),
				typeof(Image), typeof(Button), typeof(Outline));
			var rect = option.GetComponent<RectTransform>();
			rect.SetParent(parent, false);
			rect.anchorMin = new Vector2(0.5f, 1f);
			rect.anchorMax = new Vector2(0.5f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, -top);
			rect.sizeDelta = new Vector2(660f, 132f);
			var color = CapturedStarbaseFacilities.GetMapColor(type);
			option.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 0.22f);
			var outline = option.GetComponent<Outline>();
			outline.effectColor = color;
			outline.effectDistance = new Vector2(2f, -2f);
			option.GetComponent<Button>().onClick.AddListener(() => SelectFacilityType(type));
			_facilityOptions[type] = option;

			var titleText = CreateFacilityText(rect, "Title", 25, FontStyle.Bold);
			titleText.text = title;
			titleText.color = Color.Lerp(color, Color.white, 0.35f);
			SetFacilityRect(titleText.rectTransform, Vector2.zero, Vector2.one,
				new Vector2(18f, 54f), new Vector2(-18f, -8f));

			var descriptionText = CreateFacilityText(rect, "Description", 18, FontStyle.Normal);
			descriptionText.text = description;
			descriptionText.alignment = TextAnchor.UpperCenter;
			SetFacilityRect(descriptionText.rectTransform, Vector2.zero, Vector2.one,
				new Vector2(18f, 10f), new Vector2(-18f, -64f));
		}

		private static Text CreateFacilityText(Transform parent, string name, int size, FontStyle style)
		{
			var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
				.GetComponent<Text>();
			text.transform.SetParent(parent, false);
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			text.fontSize = size;
			text.fontStyle = style;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = Color.white;
			text.resizeTextForBestFit = true;
			text.resizeTextMinSize = 13;
			text.resizeTextMaxSize = size;
			return text;
		}

		private static void SetFacilityRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
			Vector2 offsetMin, Vector2 offsetMax)
		{
			rect.anchorMin = anchorMin;
			rect.anchorMax = anchorMax;
			rect.offsetMin = offsetMin;
			rect.offsetMax = offsetMax;
		}

		private void OpenFacilityTypePanel()
		{
			var region = _motherShip.CurrentStar.Region;
			if (!region.IsCaptured || _facilityTypePanel == null)
				return;
			if (_facilityTypeTierText != null)
				_facilityTypeTierText.text = $"空间站等级 {region.HomeStarLevel} · " +
					CapturedStarbaseFacilities.GetTierText(region.CapturedStarbaseTier);
			var advanced = CapturedStarbaseFacilities.IsAdvancedFacilityUnlocked(region);
			if (_facilityOptions.TryGetValue(CapturedStarbaseFacilityType.Prism, out var prism))
				prism.SetActive(advanced);
			if (_facilityOptions.TryGetValue(CapturedStarbaseFacilityType.Lane, out var lane))
				lane.SetActive(advanced);
			_facilityTypePanel.SetActive(true);
			_facilityTypePanel.transform.SetAsLastSibling();
		}

		private void SelectFacilityType(CapturedStarbaseFacilityType type)
		{
			var region = _motherShip.CurrentStar.Region;
			if (!region.IsCaptured ||
				(type >= CapturedStarbaseFacilityType.Prism &&
				 !CapturedStarbaseFacilities.IsAdvancedFacilityUnlocked(region)))
				return;
			region.CapturedStarbaseFacility = type;
			RefreshFacilityTypeButton(region);
			if (_facilityTypePanel != null)
				_facilityTypePanel.SetActive(false);
			_starContentChangedTrigger.Fire(region.HomeStar);
		}

		private void RefreshFacilityTypeButton(GameModel.Region region)
		{
			if (_facilityTypeButton == null || _facilityTypeLabel == null || region == null)
				return;
			var tier = region.CapturedStarbaseTier;
			var type = region.CapturedStarbaseFacility;
			var typeName = CapturedStarbaseFacilities.GetChineseName(type);
			string effect;
				switch (type)
			{
				case CapturedStarbaseFacilityType.Border:
					effect = $"支援舰队等级 +{tier}" + (tier >= 10 ? "，追加1艘战列舰" : string.Empty);
					break;
				case CapturedStarbaseFacilityType.Research:
					effect = $"每日 +{tier} { _localization.GetString(region.Faction.Name) }科研点";
					break;
				case CapturedStarbaseFacilityType.Prism:
					effect = CapturedStarbaseFacilities.IsPrismCharged(_session, region.Id)
						? "棱镜已充能，可攻击50光年内空间站"
						: "今日充能已使用";
					break;
				case CapturedStarbaseFacilityType.Lane:
					effect = "可传送至100光年内的航道空间站";
					break;
				default:
					effect = $"每日 +{tier * 1000}信用点 / +{tier}星币";
					break;
			}
			_facilityTypeLabel.text = $"空间站类型：{typeName}（{CapturedStarbaseFacilities.GetTierText(tier)}）\n{effect}";

			var color = CapturedStarbaseFacilities.GetMapColor(type);
			var image = _facilityTypeButton.GetComponent<Image>();
			if (image != null)
				image.color = new Color(color.r, color.g, color.b, 0.24f);
			var outline = _facilityTypeButton.GetComponent<Outline>();
			if (outline != null)
				outline.effectColor = color;
			RefreshFacilityActionButton(region);
		}

		private void SetFacilityTypeVisible(bool visible)
		{
			if (_facilityTypeButton != null)
				_facilityTypeButton.SetActive(visible);
			if (!visible && _facilityActionButton != null)
				_facilityActionButton.SetActive(false);
			if (!visible && _facilityTypePanel != null)
				_facilityTypePanel.SetActive(false);
		}

		private void EnsureFacilityActionButton()
		{
			_facilityActionButton = GetComponentsInChildren<Transform>(true)
				.FirstOrDefault(item => item.name == "StarbaseFacilityActionButton")?.gameObject;
			if (_facilityActionButton != null)
			{
				_facilityActionLabel = _facilityActionButton.GetComponentInChildren<Text>(true);
				return;
			}

			_facilityActionButton = new GameObject("StarbaseFacilityActionButton", typeof(RectTransform),
				typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
			var rect = _facilityActionButton.GetComponent<RectTransform>();
			rect.SetParent(CaptureButton.transform.parent, false);
			rect.sizeDelta = new Vector2(410f, 72f);
			var layout = _facilityActionButton.GetComponent<LayoutElement>();
			layout.minWidth = layout.preferredWidth = 410f;
			layout.minHeight = layout.preferredHeight = 72f;
			layout.flexibleWidth = 0f;
			_facilityActionButton.GetComponent<Button>().onClick.AddListener(OpenFacilityTargetPanel);

			_facilityActionLabel = CreateFacilityText(rect, "Label", 23, FontStyle.Bold);
			_facilityActionLabel.rectTransform.anchorMin = Vector2.zero;
			_facilityActionLabel.rectTransform.anchorMax = Vector2.one;
			_facilityActionLabel.rectTransform.offsetMin = new Vector2(10f, 4f);
			_facilityActionLabel.rectTransform.offsetMax = new Vector2(-10f, -4f);
			_facilityActionButton.SetActive(false);
		}

		private void RefreshFacilityActionButton(GameModel.Region region)
		{
			if (_facilityActionButton == null || _facilityActionLabel == null || region == null)
				return;

			var type = region.CapturedStarbaseFacility;
			var visible = CapturedStarbaseFacilities.IsAdvancedFacilityUnlocked(region) &&
				(type == CapturedStarbaseFacilityType.Prism || type == CapturedStarbaseFacilityType.Lane);
			_facilityActionButton.SetActive(visible);
			if (!visible)
				return;

			var charged = type != CapturedStarbaseFacilityType.Prism ||
				CapturedStarbaseFacilities.IsPrismCharged(_session, region.Id);
			_facilityActionLabel.text = type == CapturedStarbaseFacilityType.Prism
				? (charged ? "启动棱镜" : "棱镜今日已使用")
				: "打开航道";
			_facilityActionButton.GetComponent<Button>().interactable = charged;
			var color = CapturedStarbaseFacilities.GetMapColor(type);
			_facilityActionButton.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 0.28f);
			_facilityActionButton.GetComponent<Outline>().effectColor = color;
		}

		private void OpenFacilityTargetPanel()
		{
			var source = _motherShip.CurrentStar.Region;
			if (!CapturedStarbaseFacilities.IsAdvancedFacilityUnlocked(source))
				return;
			var type = source.CapturedStarbaseFacility;
			if (type != CapturedStarbaseFacilityType.Prism && type != CapturedStarbaseFacilityType.Lane)
				return;
			if (type == CapturedStarbaseFacilityType.Prism &&
				!CapturedStarbaseFacilities.IsPrismCharged(_session, source.Id))
				return;

			EnsureFacilityTargetPanel();
			foreach (Transform child in _facilityTargetContent)
				Destroy(child.gameObject);

			var range = type == CapturedStarbaseFacilityType.Prism
				? CapturedStarbaseFacilities.PrismRange
				: CapturedStarbaseFacilities.LaneRange;
			var candidates = new List<GameModel.Region>();
			_regionMap.GetAdjacentRegions(source.HomeStar, 1, range, candidates);
			var targets = candidates
				.Where(region => region != null && region != GameModel.Region.Empty && region.Id != source.Id)
				.Where(region => type == CapturedStarbaseFacilityType.Prism
					? !region.IsCaptured
					: region.IsCaptured && region.CapturedStarbaseTier >= CapturedStarbaseFacilities.MaxTier &&
					  region.CapturedStarbaseFacility == CapturedStarbaseFacilityType.Lane)
				.OrderBy(region => GameModel.StarLayout.Distance(source.HomeStar, region.HomeStar))
				.ToList();

			_facilityTargetTitle.text = type == CapturedStarbaseFacilityType.Prism
				? "选择棱镜攻击目标（50光年）"
				: "选择航道目的地（100光年）";
			if (targets.Count == 0)
			{
				var empty = CreateFacilityText(_facilityTargetContent, "Empty", 21, FontStyle.Normal);
				empty.text = type == CapturedStarbaseFacilityType.Prism
					? "范围内没有可占领的空间站"
					: "范围内没有另一座可连接的航道空间站";
				empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 90f;
			}
			else
			{
				foreach (var target in targets)
					CreateFacilityTargetButton(source, target, type);
			}

			_facilityTargetPanel.SetActive(true);
			_facilityTargetPanel.transform.SetAsLastSibling();
		}

		private void EnsureFacilityTargetPanel()
		{
			if (_facilityTargetPanel != null)
				return;

			var root = GetComponentInParent<Canvas>()?.rootCanvas?.transform ?? transform.root;
			_facilityTargetPanel = new GameObject("StarbaseFacilityTargetPanel", typeof(RectTransform),
				typeof(CanvasRenderer), typeof(Image), typeof(Button));
			var panelRect = _facilityTargetPanel.GetComponent<RectTransform>();
			panelRect.SetParent(root, false);
			panelRect.anchorMin = Vector2.zero;
			panelRect.anchorMax = Vector2.one;
			panelRect.offsetMin = Vector2.zero;
			panelRect.offsetMax = Vector2.zero;
			_facilityTargetPanel.GetComponent<Image>().color = new Color(0f, 0f, 0.025f, 0.86f);
			_facilityTargetPanel.GetComponent<Button>().onClick.AddListener(() => _facilityTargetPanel.SetActive(false));

			var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
				typeof(Outline));
			var cardRect = card.GetComponent<RectTransform>();
			cardRect.SetParent(panelRect, false);
			cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
			cardRect.sizeDelta = new Vector2(720f, 720f);
			card.GetComponent<Image>().color = new Color(0.025f, 0.012f, 0.065f, 0.98f);
			card.GetComponent<Outline>().effectColor = new Color(0.65f, 0.38f, 1f, 0.95f);

			_facilityTargetTitle = CreateFacilityText(cardRect, "Title", 28, FontStyle.Bold);
			SetFacilityRect(_facilityTargetTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
				new Vector2(20f, -86f), new Vector2(-20f, -18f));

			var directionLegend = CreateFacilityText(cardRect, "DirectionLegend", 18, FontStyle.Normal);
			directionLegend.text = "↑ 上　↗ 右上　→ 右　↘ 右下　↓ 下　↙ 左下　← 左　↖ 左上";
			directionLegend.color = new Color(0.72f, 0.9f, 1f, 0.92f);
			SetFacilityRect(directionLegend.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
				new Vector2(24f, -128f), new Vector2(-24f, -88f));

			var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
				typeof(Mask));
			var viewportRect = viewport.GetComponent<RectTransform>();
			viewportRect.SetParent(cardRect, false);
			SetFacilityRect(viewportRect, Vector2.zero, Vector2.one, new Vector2(28f, 34f), new Vector2(-28f, -142f));
			viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
			viewport.GetComponent<Mask>().showMaskGraphic = true;

			var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
				typeof(ContentSizeFitter));
			_facilityTargetContent = content.GetComponent<RectTransform>();
			_facilityTargetContent.SetParent(viewportRect, false);
			_facilityTargetContent.anchorMin = new Vector2(0f, 1f);
			_facilityTargetContent.anchorMax = new Vector2(1f, 1f);
			_facilityTargetContent.pivot = new Vector2(0.5f, 1f);
			_facilityTargetContent.offsetMin = Vector2.zero;
			_facilityTargetContent.offsetMax = Vector2.zero;
			var vertical = content.GetComponent<VerticalLayoutGroup>();
			vertical.padding = new RectOffset(12, 12, 12, 12);
			vertical.spacing = 10f;
			vertical.childControlHeight = true;
			vertical.childForceExpandHeight = false;
			content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			var scroll = viewport.AddComponent<ScrollRect>();
			scroll.viewport = viewportRect;
			scroll.content = _facilityTargetContent;
			scroll.horizontal = false;
			scroll.vertical = true;
			scroll.movementType = ScrollRect.MovementType.Clamped;
			_facilityTargetPanel.SetActive(false);
		}

		private void CreateFacilityTargetButton(GameModel.Region source, GameModel.Region target,
			CapturedStarbaseFacilityType type)
		{
			var buttonObject = new GameObject("Target_" + target.Id, typeof(RectTransform),
				typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
			buttonObject.transform.SetParent(_facilityTargetContent, false);
			buttonObject.GetComponent<LayoutElement>().preferredHeight = 86f;
			var color = CapturedStarbaseFacilities.GetMapColor(type);
			buttonObject.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 0.22f);
			buttonObject.GetComponent<Outline>().effectColor = color;
			var distance = GameModel.StarLayout.Distance(source.HomeStar, target.HomeStar);
			var direction = GetFacilityDirectionHint(source.HomeStar, target.HomeStar);
			var label = CreateFacilityText(buttonObject.transform, "Label", 21, FontStyle.Bold);
			label.rectTransform.anchorMin = Vector2.zero;
			label.rectTransform.anchorMax = Vector2.one;
			label.rectTransform.offsetMin = new Vector2(12f, 4f);
			label.rectTransform.offsetMax = new Vector2(-12f, -4f);
			label.text = $"{direction}　{_localization.GetString(target.Faction.Name)}空间站  ·  {distance:0.0}光年";
			buttonObject.GetComponent<Button>().onClick.AddListener(() => ActivateFacilityTarget(source, target, type));
		}

		private string GetFacilityDirectionHint(int sourceStarId, int targetStarId)
		{
			var source = GameModel.StarLayout.GetStarPosition(sourceStarId, _session.Game.Seed);
			var target = GameModel.StarLayout.GetStarPosition(targetStarId, _session.Game.Seed);
			var delta = target - source;
			if (delta.sqrMagnitude < 0.0001f)
				return "• 同位置";

			var sector = Mathf.RoundToInt(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg / 45f);
			sector = (sector % 8 + 8) % 8;
			switch (sector)
			{
				case 0: return "→ 右";
				case 1: return "↗ 右上";
				case 2: return "↑ 上";
				case 3: return "↖ 左上";
				case 4: return "← 左";
				case 5: return "↙ 左下";
				case 6: return "↓ 下";
				default: return "↘ 右下";
			}
		}

		private void ActivateFacilityTarget(GameModel.Region source, GameModel.Region target,
			CapturedStarbaseFacilityType type)
		{
			if (type == CapturedStarbaseFacilityType.Prism)
			{
				if (!CapturedStarbaseFacilities.TryConsumePrismCharge(_session, source.Id))
					return;
				_messenger.Broadcast<int, int>(EventType.PrismBeamFired, source.HomeStar, target.HomeStar);
				target.IsCaptured = true;
				_starContentChangedTrigger.Fire(target.HomeStar);
				RefreshFacilityTypeButton(source);
			}
			else if (type == CapturedStarbaseFacilityType.Lane)
			{
				_motherShip.ViewMode = ViewMode.StarMap;
				_motherShip.Position = target.HomeStar;
			}

			if (_facilityTargetPanel != null)
				_facilityTargetPanel.SetActive(false);
		}

        private void SetJointControlsVisible(bool visible)
        {
            if (_jointAttackButton != null)
                _jointAttackButton.SetActive(visible);
            if (!visible && _alliedAttackPanel != null)
                _alliedAttackPanel.SetActive(false);
        }

        private GameObject _jointAttackButton;
        private GameObject _alliedAttackPanel;
        private GameObject _peacefulTransferButton;
        private GameObject _defenseButton;
		private GameObject _facilityTypeButton;
		private GameObject _facilityTypePanel;
		private Text _facilityTypeLabel;
		private Text _facilityTypeTierText;
		private readonly Dictionary<CapturedStarbaseFacilityType, GameObject> _facilityOptions =
			new Dictionary<CapturedStarbaseFacilityType, GameObject>();
		private GameObject _facilityActionButton;
		private Text _facilityActionLabel;
		private GameObject _facilityTargetPanel;
		private RectTransform _facilityTargetContent;
		private Text _facilityTargetTitle;
	}
}
