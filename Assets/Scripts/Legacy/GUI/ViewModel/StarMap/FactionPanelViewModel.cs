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

namespace ViewModel
{
	public class FactionPanelViewModel : MonoBehaviour
	{
        [Inject] private readonly ItemTypeFactory _factory;
        [Inject] private readonly IMessenger _messenger;
	    [Inject] private readonly MotherShip _motherShip;
	    [Inject] private readonly IQuestManager _questManager;
	    [Inject] private readonly OpenShopSignal.Trigger _openShopTrigger;
	    [Inject] private readonly InventoryFactory _inventoryFactory;
	    [Inject] private readonly ISessionData _session;
	    [Inject] private readonly ILocalization _localization;
        [Inject] private readonly QuestEventSignal.Trigger _questEventTrigger;
	    [Inject] private readonly StartBattleSignal.Trigger _startBattleTrigger;

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
			var region = _motherShip.CurrentStar.Region;

		    FactionName.text = _localization.GetString(region.Faction.Name);
            FactionName.color = region.Faction.Color;

		    var reputation = _session.Quests.GetFactionRelations(region.HomeStar);

            if (region.IsCaptured)
		    {
                SetJointControlsVisible(false);
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
            SetJointControlsVisible(true);
		    CaptureDescription.gameObject.SetActive(true);
		    MilitaryPowerPanel.gameObject.SetActive(true);
		    ReputationPanel.gameObject.SetActive(!region.Faction.NoMissions);
		    ReputationText.text = reputation > 0 ? "+" + reputation : reputation.ToString();
		    PowerText.text = region.BaseDefensePower + "%";

            MissionButton.gameObject.SetActive(MissionsAvailable);

			ShopButton.SetActive(reputation >= 5);
			CraftButton.SetActive(reputation >= 60);
            ShipyardButton.SetActive(reputation >= 90);
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
                buttonsRect.sizeDelta = new Vector2(buttonsRect.sizeDelta.x, Mathf.Max(260f, buttonsRect.sizeDelta.y));
            var captureLayout = CaptureButton.GetComponent<LayoutElement>() ?? CaptureButton.AddComponent<LayoutElement>();
            captureLayout.preferredHeight = 58f;
            captureLayout.flexibleWidth = 1f;

            var emblem = transform.Find("Body/Left/Faction") ?? transform.Find("Faction");
            if (emblem != null)
                emblem.localScale = Vector3.one * 0.72f;

            _alliedAttackPanel = GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "Preview5AlliedAttackList")?.gameObject;
            _jointAttackButton = GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "Preview5JointAttackButton")?.gameObject;
            if (_alliedAttackPanel != null && _jointAttackButton != null)
                return;

            var panel = new GameObject("Preview5AlliedAttackList", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = panel.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-25f, 24f);
            rect.sizeDelta = new Vector2(430f, 52f);
            panel.GetComponent<Image>().color = new Color(0.02f, 0.08f, 0.14f, 0.9f);
            panel.SetActive(false);
            _alliedAttackPanel = panel;

            var toggleObject = new GameObject("StarshipEarth", typeof(RectTransform), typeof(Toggle));
            var toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.SetParent(rect, false);
            toggleRect.anchorMin = Vector2.zero;
            toggleRect.anchorMax = Vector2.one;
            toggleRect.offsetMin = new Vector2(12f, 4f);
            toggleRect.offsetMax = new Vector2(-12f, -4f);

            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var checkRect = check.GetComponent<RectTransform>();
            checkRect.SetParent(toggleRect, false);
            checkRect.anchorMin = checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.pivot = new Vector2(0f, 0.5f);
            checkRect.sizeDelta = new Vector2(30f, 30f);
            checkRect.anchoredPosition = Vector2.zero;
            check.GetComponent<Image>().color = new Color(0.1f, 0.55f, 1f, 1f);

            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.SetParent(toggleRect, false);
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(44f, 0f);
            labelRect.offsetMax = Vector2.zero;
            var labelText = label.GetComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 22;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = Color.white;
            labelText.text = "联合进攻盟友：星舰地球";

            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = panel.GetComponent<Image>();
            toggle.isOn = IncludeStarshipEarthAllies;
            toggle.onValueChanged.AddListener(value => IncludeStarshipEarthAllies = value);

            var jointObject = new GameObject("Preview5JointAttackButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var jointRect = jointObject.GetComponent<RectTransform>();
            jointRect.SetParent(CaptureButton.transform.parent, false);
            jointObject.transform.SetAsLastSibling();
            jointRect.sizeDelta = new Vector2(captureRect.sizeDelta.x, 58f);
            var jointLayout = jointObject.AddComponent<LayoutElement>();
            jointLayout.preferredHeight = 58f;
            jointLayout.flexibleWidth = 1f;
            var captureImage = CaptureButton.GetComponent<Image>();
            var jointImage = jointObject.GetComponent<Image>();
            if (captureImage != null)
            {
                jointImage.sprite = captureImage.sprite;
                jointImage.type = captureImage.type;
                jointImage.color = captureImage.color;
            }
            jointObject.GetComponent<Button>().onClick.AddListener(() =>
            {
                panel.SetActive(!panel.activeSelf);
                if (panel.activeSelf) panel.transform.SetAsLastSibling();
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
            jointText.text = "联合进攻";
            _jointAttackButton = jointObject;
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
	}
}
