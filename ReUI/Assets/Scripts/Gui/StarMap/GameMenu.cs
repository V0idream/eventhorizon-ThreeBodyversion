using Game;
using UnityEngine;
using UnityEngine.UI;
using GameServices.Player;
using GameStateMachine.States;
using Services.Messenger;
using Gui.Windows;
using Services.Gui;
using Zenject;
using System.Linq;
using GameDatabase;
using Combat.Component.Unit.Classification;
using Services.Localization;
using Session;
using System.Collections.Generic;
using GameServices.Captains;
using Gui.Common;

namespace Gui.StarMap
{
    public class GameMenu : MonoBehaviour
    {
        [Inject] private readonly MotherShip _motherShip;
        [Inject] private readonly ExitSignal.Trigger _exitTrigger;
        [Inject] private readonly Galaxy.StarMap _starMap;
        [Inject] private readonly HolidayManager _holidayManager;
        [Inject] private readonly IMessenger _messenger;
        [Inject] private readonly IDatabase _database;
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly ISessionData _session;
        [Inject] private readonly GameModel.RegionMap _regionMap;
        [Inject] private readonly CaptainService _captains;

        public AnimatedWindow InformationPanel;
        public AnimatedWindow CargoHoldPanel;
        public AnimatedWindow FleetPanel;
        public AnimatedWindow ResearchPanel;

        public AnimatedWindow SurvivalPanel;
        public AnimatedWindow ArenaPanel;
        public AnimatedWindow RuinsPanel;
        public AnimatedWindow XmasPanel;
        public AnimatedWindow MilitaryPanel;
        public AnimatedWindow PlanetPanel;
        public AnimatedWindow BossPanel;
        public AnimatedWindow FactionPanel;
        public AnimatedWindow WormholePanel;
        public AnimatedWindow BlackMarketPanel;
        public AnimatedWindow ChallengePanel;
        public AnimatedWindow IapStoreWindow;
        public AnimatedWindow QuestLogWindow;

        [SerializeField] private Button StarViewButton;
        [SerializeField] private Button GalaxyViewButton;
        [SerializeField] private GameObject GalaxyButtonsGroup;
        [SerializeField] private GameObject FiltersGroup;
        [SerializeField] private Toggle BookmarkFilterToggle;
        [SerializeField] private Toggle BossFilterToggle;
        [SerializeField] private Toggle ShopFilterToggle;
        [SerializeField] private Toggle ArenaFilterToggle;
        [SerializeField] private Toggle XmasFilterToggle;
        
        public void ShowInformation() { InformationPanel.Open(); }
        public void ShowCargoHold() { CargoHoldPanel.Open(); }
        public void ShowFleet() { FleetPanel.Open(); }
        public void ShowResearch() { ResearchPanel.Open(); }
        public void ShowSurvival() { SurvivalPanel.Open(); }
        public void ShowArena() { ArenaPanel.Open(); }
        public void ShowRuins() { RuinsPanel.Open(); }
        public void ShowXmas() { XmasPanel.Open(); }
        public void ShowMilitaryBase() { MilitaryPanel.Open(); }
        public void ShowPandemic() { PlanetPanel.Open(new WindowArgs(Game.Exploration.Planet.InfectedPlanetId)); }
        public void ShowPlanet(int id) { PlanetPanel.Open(new WindowArgs(id)); }
        public void ShowBoss() { BossPanel.Open(); }
        public void ShowFaction() { FactionPanel.Open(); }
        public void ShowWormhole() { WormholePanel.Open(); }
        public void ShowBlackMarket() { BlackMarketPanel.Open(); }
        public void ShowChallenge() { ChallengePanel.Open(); }
        public void ShowIapStore() { IapStoreWindow.Open(); }
        public void ShowQuestLog() { QuestLogWindow.Open(); }

        public void ExitToMainMenu()
        {
            _exitTrigger.Fire();
        }

        public void OnFiltersChanged()
        {
            _starMap.ShowBosses = BossFilterToggle.isOn;
            _starMap.ShowStores = ShopFilterToggle.isOn;
            _starMap.ShowBookmarks = BookmarkFilterToggle.isOn;
            _starMap.ShowArenas = ArenaFilterToggle.isOn;
            _starMap.ShowXmas = XmasFilterToggle.isOn && _holidayManager.IsChristmas;
            _messenger.Broadcast(EventType.StarMapContentChanged);
        }

        private void Start()
        {
            ThreeBodyUiPalette.Configure(_database.UiSettings);
            ApplyPreview4FactionIcon();
            CreateRelationsButton();
            CreateCaptainButton();
            CreateStorylineButton();
            HidePremiumBuyButton();
            _messenger.AddListener<int>(EventType.PlayerPositionChanged, OnPlayerPositionChanged);
            _messenger.AddListener<ViewMode>(EventType.ViewModeChanged, OnMapStateChanged);
            _messenger.AddListener<Galaxy.StarObjectType>(EventType.ArrivedToObject, OnArrivedToObject);
            _messenger.AddListener<int>(EventType.ArrivedToPlanet, OnArrivedToPlanet);

            XmasFilterToggle.gameObject.SetActive(_holidayManager.IsChristmas);

            InitButtons();
            OnFiltersChanged();
        }

        private void CreateRelationsButton()
        {
            if (transform.Find("Preview5RelationsButton") != null) return;
            var exit = GetComponentsInChildren<Button>(true).FirstOrDefault(button =>
            {
                for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    if (button.onClick.GetPersistentMethodName(i) == nameof(ExitToMainMenu)) return true;
                return false;
            });
            if (exit == null) return;

            var buttonObject = new GameObject("Preview5RelationsButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(exit.transform.parent, false);
            buttonObject.name = "Preview5RelationsButton";
            var rect = buttonObject.GetComponent<RectTransform>();
            var exitRect = exit.GetComponent<RectTransform>();
            rect.anchorMin = exitRect.anchorMin;
            rect.anchorMax = exitRect.anchorMax;
            rect.pivot = exitRect.pivot;
            rect.sizeDelta = exitRect.sizeDelta;
            rect.anchoredPosition = exitRect.anchoredPosition + new Vector2(exitRect.rect.width + 12f, 0f);
            var sourceLayout = exit.GetComponent<LayoutElement>();
            var buttonLayout = buttonObject.GetComponent<LayoutElement>();
            if (sourceLayout != null)
            {
                buttonLayout.minWidth = sourceLayout.minWidth;
                buttonLayout.minHeight = sourceLayout.minHeight;
                buttonLayout.preferredWidth = sourceLayout.preferredWidth;
                buttonLayout.preferredHeight = sourceLayout.preferredHeight;
                buttonLayout.flexibleWidth = sourceLayout.flexibleWidth;
                buttonLayout.flexibleHeight = sourceLayout.flexibleHeight;
                buttonLayout.layoutPriority = sourceLayout.layoutPriority;
            }
            else
            {
                buttonLayout.preferredWidth = 120f;
                buttonLayout.preferredHeight = 120f;
            }
            var sourceImage = exit.GetComponent<Image>();
            var image = buttonObject.GetComponent<Image>();
            if (sourceImage != null)
            {
                image.sprite = sourceImage.sprite;
                image.type = sourceImage.type;
                image.color = sourceImage.color;
            }
            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(ToggleRelationsPanel);
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(buttonObject.transform, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(10f, 10f);
            iconRect.offsetMax = new Vector2(-10f, -10f);
            var iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = Resources.Load<Sprite>("Textures/UI/faction_relations_preview4");
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }

        private void CreateCaptainButton()
        {
            // The faction shortcut belongs to the star-map canvas rather than to the
            // GameMenu hierarchy in some scene variants, so look through the canvas.
            var canvasRoot = transform.root;
            var buttons = canvasRoot.GetComponentsInChildren<Button>(true);
            var existingCaptainButton = buttons.FirstOrDefault(button => button.name == "ThreeBodyCaptainButton");
            if (existingCaptainButton != null)
            {
                EnsureCaptainIcon(existingCaptainButton);
                return;
            }

            // Preview5RelationsButton is the faction shortcut created above.  It
            // has a runtime listener instead of a serialized ShowFaction event,
            // so the old persistent-event search could not find it and silently
            // skipped creating the captain entry.
            var factionButton = buttons.FirstOrDefault(button => button.name == "Preview5RelationsButton")
                                ?? buttons.FirstOrDefault(button =>
                                {
                                    for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                                    {
                                        if (button.onClick.GetPersistentMethodName(i) == nameof(ShowFaction) ||
                                            ReferenceEquals(button.onClick.GetPersistentTarget(i), FactionPanel))
                                            return true;
                                    }
                                    return false;
                                });
            if (factionButton == null)
                return;

            var buttonObject = new GameObject("ThreeBodyCaptainButton", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.layer = factionButton.gameObject.layer;
            buttonObject.transform.SetParent(factionButton.transform.parent, false);
            buttonObject.transform.SetSiblingIndex(factionButton.transform.GetSiblingIndex() + 1);

            var sourceRect = factionButton.GetComponent<RectTransform>();
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = sourceRect.pivot;
            rect.sizeDelta = sourceRect.sizeDelta;
            rect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(sourceRect.rect.width + 12f, 0f);

            var sourceLayout = factionButton.GetComponent<LayoutElement>();
            var layout = buttonObject.GetComponent<LayoutElement>();
            if (sourceLayout != null)
            {
                layout.minWidth = sourceLayout.minWidth;
                layout.minHeight = sourceLayout.minHeight;
                layout.preferredWidth = sourceLayout.preferredWidth;
                layout.preferredHeight = sourceLayout.preferredHeight;
                layout.flexibleWidth = sourceLayout.flexibleWidth;
                layout.flexibleHeight = sourceLayout.flexibleHeight;
                layout.layoutPriority = sourceLayout.layoutPriority;
            }

            var image = buttonObject.GetComponent<Image>();
            var sourceImage = factionButton.GetComponent<Image>();
            image.sprite = sourceImage != null ? sourceImage.sprite : null;
            image.type = sourceImage != null ? sourceImage.type : Image.Type.Sliced;
            image.color = ThreeBodyUiPalette.Button;

            var button = buttonObject.GetComponent<Button>();
            EnsureCaptainIcon(button);
            button.onClick.AddListener(ToggleCaptainPanel);
        }

        private void CreateStorylineButton()
        {
            var canvasRoot = transform.root;
            var buttons = canvasRoot.GetComponentsInChildren<Button>(true);
            var existing = buttons.FirstOrDefault(button => button.name == "ThreeBodyStorylineButton");
            if (existing != null)
            {
                EnsureStorylineIcon(existing);
                return;
            }

            var captainButton = buttons.FirstOrDefault(button => button.name == "ThreeBodyCaptainButton");
            if (captainButton == null)
                return;

            var buttonObject = new GameObject("ThreeBodyStorylineButton", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.layer = captainButton.gameObject.layer;
            buttonObject.transform.SetParent(captainButton.transform.parent, false);
            buttonObject.transform.SetSiblingIndex(captainButton.transform.GetSiblingIndex() + 1);

            var sourceRect = captainButton.GetComponent<RectTransform>();
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = sourceRect.pivot;
            rect.sizeDelta = sourceRect.sizeDelta;
            rect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(sourceRect.rect.width + 12f, 0f);

            var sourceLayout = captainButton.GetComponent<LayoutElement>();
            var layout = buttonObject.GetComponent<LayoutElement>();
            if (sourceLayout != null)
            {
                layout.minWidth = sourceLayout.minWidth;
                layout.minHeight = sourceLayout.minHeight;
                layout.preferredWidth = sourceLayout.preferredWidth;
                layout.preferredHeight = sourceLayout.preferredHeight;
                layout.flexibleWidth = sourceLayout.flexibleWidth;
                layout.flexibleHeight = sourceLayout.flexibleHeight;
                layout.layoutPriority = sourceLayout.layoutPriority;
            }

            var sourceImage = captainButton.GetComponent<Image>();
            var image = buttonObject.GetComponent<Image>();
            image.sprite = sourceImage != null ? sourceImage.sprite : null;
            image.type = sourceImage != null ? sourceImage.type : Image.Type.Sliced;
            image.color = ThreeBodyUiPalette.Button;

            var button = buttonObject.GetComponent<Button>();
            EnsureStorylineIcon(button);
            button.onClick.AddListener(ToggleStorylinePanel);
        }

        private static void EnsureCaptainIcon(Button button)
        {
            if (button == null)
                return;

            // A previous ReUI pass could leave a generated vector host below
            // this button.  The captain shortcut is intentionally raster-only:
            // remove that stale host rather than merely hiding it.
            foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null && graphic.GetType().Name == "ReUIIconGraphic")
                    UnityEngine.Object.Destroy(graphic.gameObject);
            }

            var label = button.transform.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);

            var iconTransform = button.transform.Find("Icon");
            Image icon;
            if (iconTransform == null)
            {
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image));
                iconObject.layer = button.gameObject.layer;
                iconTransform = iconObject.transform;
                iconTransform.SetParent(button.transform, false);
                icon = iconObject.GetComponent<Image>();
            }
            else
            {
                icon = iconTransform.GetComponent<Image>();
                if (icon == null)
                    icon = iconTransform.gameObject.AddComponent<Image>();
            }

            foreach (var other in iconTransform.GetComponents<Graphic>())
                if (other != icon)
                    other.enabled = false;

            var rect = icon.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 10f);
            rect.offsetMax = new Vector2(-10f, -10f);
            rect.localScale = Vector3.one;

            icon.sprite = ThreeBodyUiPalette.LoadCaptainIcon();
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.maskable = false;
            icon.canvasRenderer.SetAlpha(1f);
            icon.gameObject.SetActive(icon.sprite != null);
            icon.enabled = icon.sprite != null;
        }

        private static void EnsureStorylineIcon(Button button)
        {
            if (button == null)
                return;

            foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null && graphic.GetType().Name == "ReUIIconGraphic")
                    UnityEngine.Object.Destroy(graphic.gameObject);
            }

            var label = button.transform.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);

            var iconTransform = button.transform.Find("Icon");
            Image icon;
            if (iconTransform == null)
            {
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.layer = button.gameObject.layer;
                iconTransform = iconObject.transform;
                iconTransform.SetParent(button.transform, false);
                icon = iconObject.GetComponent<Image>();
            }
            else
            {
                icon = iconTransform.GetComponent<Image>();
                if (icon == null)
                    icon = iconTransform.gameObject.AddComponent<Image>();
            }

            var rect = icon.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 10f);
            rect.offsetMax = new Vector2(-10f, -10f);
            rect.localScale = Vector3.one;

            icon.sprite = Resources.Load<Sprite>("Textures/GUI/quest");
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.maskable = false;
            icon.canvasRenderer.SetAlpha(1f);
            icon.gameObject.SetActive(icon.sprite != null);
            icon.enabled = icon.sprite != null;
        }

        private void HidePremiumBuyButton()
        {
            var buyButton = transform.root.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "BuyButton" &&
                                          button.transform.parent != null &&
                                          button.transform.parent.name == "StatusPanel");
            if (buyButton != null)
                buyButton.gameObject.SetActive(false);
        }

        private void ToggleCaptainPanel()
        {
            if (_captainPanel != null)
            {
                _captainPanel.SetActive(!_captainPanel.activeSelf);
                if (_captainPanel.activeSelf)
                {
                    if (_storylinePanel != null)
                        _storylinePanel.SetActive(false);
                    RefreshCaptainCards();
                }
                return;
            }

            var root = transform.root as RectTransform;
            if (root == null)
                return;

            _captainPanel = new GameObject("CaptainPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _captainPanel.layer = gameObject.layer;
            var panelRect = _captainPanel.GetComponent<RectTransform>();
            panelRect.SetParent(root, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1020f, 660f);
            _captainPanel.GetComponent<Image>().color = ThreeBodyUiPalette.PanelDeep;
            _captainPanel.transform.SetAsLastSibling();

            var title = NewCaptainText(panelRect, "Title", "舰长", 34, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0.04f, 0.86f);
            title.rectTransform.anchorMax = new Vector2(0.96f, 0.98f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            title.color = ThreeBodyUiPalette.AccentSoft;

            var subtitle = NewCaptainText(panelRect, "Subtitle", "选择一名舰长。舰长技能只在下一场及之后的战斗中生效。", 18, TextAnchor.MiddleCenter);
            subtitle.rectTransform.anchorMin = new Vector2(0.04f, 0.79f);
            subtitle.rectTransform.anchorMax = new Vector2(0.96f, 0.87f);
            subtitle.rectTransform.offsetMin = subtitle.rectTransform.offsetMax = Vector2.zero;
            subtitle.color = ThreeBodyUiPalette.TextMuted;

            _captainContent = new GameObject("CaptainList", typeof(RectTransform));
            var contentRect = _captainContent.GetComponent<RectTransform>();
            contentRect.SetParent(panelRect, false);
            contentRect.anchorMin = new Vector2(0.04f, 0.14f);
            contentRect.anchorMax = new Vector2(0.96f, 0.77f);
            contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;
            var listLayout = _captainContent.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 14f;
            listLayout.childAlignment = TextAnchor.UpperCenter;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;

            var close = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            close.transform.SetParent(panelRect, false);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.34f, 0.03f);
            closeRect.anchorMax = new Vector2(0.66f, 0.12f);
            closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
            close.GetComponent<Image>().color = ThreeBodyUiPalette.Button;
            close.GetComponent<Button>().onClick.AddListener(() => _captainPanel.SetActive(false));
            var closeText = NewCaptainText(close.transform, "Text", "关闭", 22, TextAnchor.MiddleCenter);
            closeText.rectTransform.anchorMin = Vector2.zero;
            closeText.rectTransform.anchorMax = Vector2.one;
            closeText.rectTransform.offsetMin = closeText.rectTransform.offsetMax = Vector2.zero;

            RefreshCaptainCards();
        }

        private void ToggleStorylinePanel()
        {
            if (_storylinePanel != null)
            {
                var show = !_storylinePanel.activeSelf;
                _storylinePanel.SetActive(show);
                if (show)
                {
                    if (_captainPanel != null)
                        _captainPanel.SetActive(false);
                    _storylinePanel.transform.SetAsLastSibling();
                    RefreshStorylineStatus();
                }
                return;
            }

            var root = transform.root as RectTransform;
            if (root == null)
                return;

            if (_captainPanel != null)
                _captainPanel.SetActive(false);

            _storylinePanel = new GameObject("StorylinePanel", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Outline));
            _storylinePanel.layer = gameObject.layer;
            var panelRect = _storylinePanel.GetComponent<RectTransform>();
            panelRect.SetParent(root, false);
            panelRect.anchorMin = new Vector2(0.09f, 0.11f);
            panelRect.anchorMax = new Vector2(0.72f, 0.92f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            _storylinePanel.GetComponent<Image>().color = ThreeBodyUiPalette.PanelDeep;
            var outline = _storylinePanel.GetComponent<Outline>();
            outline.effectColor = new Color(0.43f, 0.78f, 1f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);
            _storylinePanel.transform.SetAsLastSibling();

            var title = NewCaptainText(panelRect, "Title", "剧情线", 34, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0.04f, 0.91f);
            title.rectTransform.anchorMax = new Vector2(0.96f, 0.99f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            title.color = ThreeBodyUiPalette.AccentSoft;

            var subtitle = NewCaptainText(panelRect, "Subtitle",
                "从“前进四”启程；完成后，两条可独立推进的支线将同时展开。", 18,
                TextAnchor.MiddleCenter);
            subtitle.rectTransform.anchorMin = new Vector2(0.04f, 0.855f);
            subtitle.rectTransform.anchorMax = new Vector2(0.96f, 0.915f);
            subtitle.rectTransform.offsetMin = subtitle.rectTransform.offsetMax = Vector2.zero;
            subtitle.color = ThreeBodyUiPalette.TextMuted;

            CreateStorylineConnector(panelRect, "RootLine", new Vector2(0.4975f, 0.48f),
                new Vector2(0.5025f, 0.59f));
            CreateStorylineConnector(panelRect, "BranchLine", new Vector2(0.25f, 0.475f),
                new Vector2(0.75f, 0.485f));
            CreateStorylineConnector(panelRect, "LeftLine", new Vector2(0.2475f, 0.43f),
                new Vector2(0.2525f, 0.48f));
            CreateStorylineConnector(panelRect, "RightLine", new Vector2(0.7475f, 0.43f),
                new Vector2(0.7525f, 0.48f));

            CreateStorylineCard(panelRect, 202, "前进四", "主线起点",
                "离开太阳系，驱动母舰驶向星辰大海。完成这段航程后，两条独立的任务线将同时展开。",
                "Textures/ThreeBodyPrologue/story_06", new Vector2(0.20f, 0.59f),
                new Vector2(0.80f, 0.845f), false, true);
            CreateStorylineCard(panelRect, 203, "圣母的眼泪", "支线任务",
                "夺取 SIM 样本与技术资料，追踪水滴样本，并在无主星系迎战三体泰坦。",
                "Story/MothersTears/story_01", new Vector2(0.035f, 0.10f),
                new Vector2(0.485f, 0.43f), true, false);
            CreateStorylineCard(panelRect, 204, "万年风雪", "支线任务",
                "调查来自旧时代的异常信号，清剿感染巢穴，破译 2001 机密文件，找回被岁月掩埋的航迹。",
                "Story/BeautifulProminence/story_01", new Vector2(0.515f, 0.10f),
                new Vector2(0.965f, 0.43f), true, false);

            var close = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button));
            close.layer = gameObject.layer;
            close.transform.SetParent(panelRect, false);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.37f, 0.018f);
            closeRect.anchorMax = new Vector2(0.63f, 0.08f);
            closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
            close.GetComponent<Image>().color = ThreeBodyUiPalette.Button;
            close.GetComponent<Button>().onClick.AddListener(() => _storylinePanel.SetActive(false));
            var closeText = NewCaptainText(close.transform, "Text", "关闭", 21, TextAnchor.MiddleCenter);
            closeText.rectTransform.anchorMin = Vector2.zero;
            closeText.rectTransform.anchorMax = Vector2.one;
            closeText.rectTransform.offsetMin = closeText.rectTransform.offsetMax = Vector2.zero;

            RefreshStorylineStatus();
        }

        private void CreateStorylineCard(RectTransform parent, int questId, string title, string category,
            string description, string imagePath, Vector2 anchorMin, Vector2 anchorMax, bool requiresRoot,
            bool horizontal)
        {
            var card = new GameObject("StoryQuest_" + questId, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Outline));
            card.layer = gameObject.layer;
            card.transform.SetParent(parent, false);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = anchorMin;
            cardRect.anchorMax = anchorMax;
            cardRect.offsetMin = cardRect.offsetMax = Vector2.zero;
            var background = card.GetComponent<Image>();
            background.color = ThreeBodyUiPalette.PanelSoft;
            var outline = card.GetComponent<Outline>();
            outline.effectColor = new Color(0.38f, 0.72f, 1f, 0.48f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var coverObject = new GameObject("Cover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coverObject.layer = gameObject.layer;
            coverObject.transform.SetParent(cardRect, false);
            var cover = coverObject.GetComponent<Image>();
            cover.sprite = LoadStorySprite(imagePath);
            cover.color = Color.white;
            cover.preserveAspect = true;
            cover.raycastTarget = false;
            cover.rectTransform.anchorMin = horizontal ? new Vector2(0.02f, 0.08f) : new Vector2(0.025f, 0.49f);
            cover.rectTransform.anchorMax = horizontal ? new Vector2(0.43f, 0.92f) : new Vector2(0.975f, 0.965f);
            cover.rectTransform.offsetMin = cover.rectTransform.offsetMax = Vector2.zero;

            var categoryText = NewCaptainText(cardRect, "Category", category, horizontal ? 15 : 14,
                TextAnchor.MiddleLeft);
            categoryText.rectTransform.anchorMin = horizontal ? new Vector2(0.47f, 0.69f) : new Vector2(0.04f, 0.37f);
            categoryText.rectTransform.anchorMax = horizontal ? new Vector2(0.69f, 0.88f) : new Vector2(0.34f, 0.48f);
            categoryText.rectTransform.offsetMin = categoryText.rectTransform.offsetMax = Vector2.zero;
            categoryText.color = ThreeBodyUiPalette.Accent;

            var titleText = NewCaptainText(cardRect, "QuestTitle", title, horizontal ? 27 : 23,
                TextAnchor.MiddleLeft);
            titleText.rectTransform.anchorMin = horizontal ? new Vector2(0.47f, 0.43f) : new Vector2(0.04f, 0.25f);
            titleText.rectTransform.anchorMax = horizontal ? new Vector2(0.94f, 0.73f) : new Vector2(0.62f, 0.40f);
            titleText.rectTransform.offsetMin = titleText.rectTransform.offsetMax = Vector2.zero;
            titleText.color = Color.white;

            var status = NewCaptainText(cardRect, "Status", string.Empty, horizontal ? 17 : 15,
                TextAnchor.MiddleRight);
            status.rectTransform.anchorMin = horizontal ? new Vector2(0.70f, 0.69f) : new Vector2(0.58f, 0.27f);
            status.rectTransform.anchorMax = horizontal ? new Vector2(0.96f, 0.88f) : new Vector2(0.96f, 0.40f);
            status.rectTransform.offsetMin = status.rectTransform.offsetMax = Vector2.zero;

            var descriptionText = NewCaptainText(cardRect, "Description", description, horizontal ? 16 : 15,
                TextAnchor.UpperLeft);
            descriptionText.rectTransform.anchorMin = horizontal ? new Vector2(0.47f, 0.08f) : new Vector2(0.04f, 0.035f);
            descriptionText.rectTransform.anchorMax = horizontal ? new Vector2(0.96f, 0.45f) : new Vector2(0.96f, 0.25f);
            descriptionText.rectTransform.offsetMin = descriptionText.rectTransform.offsetMax = Vector2.zero;
            descriptionText.color = new Color(0.90f, 0.90f, 0.96f);

            _storylineCards[questId] = new StorylineCardView
            {
                QuestId = questId,
                RequiresRoot = requiresRoot,
                Background = background,
                Cover = cover,
                Status = status,
                Outline = outline
            };
        }

        private static void CreateStorylineConnector(RectTransform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var connector = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            connector.transform.SetParent(parent, false);
            var rect = connector.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            connector.GetComponent<Image>().color = new Color(0.35f, 0.82f, 1f, 0.72f);
        }

        private void RefreshStorylineStatus()
        {
            var rootCompleted = _session.Quests.HasBeenCompleted(202);
            foreach (var pair in _storylineCards)
            {
                var view = pair.Value;
                var unlocked = !view.RequiresRoot || rootCompleted;
                Color statusColor;
                if (!unlocked)
                {
                    view.Status.text = "完成“前进四”后解锁";
                    statusColor = ThreeBodyUiPalette.TextMuted;
                }
                else if (_session.Quests.HasBeenCompleted(view.QuestId))
                {
                    view.Status.text = "已完成";
                    statusColor = new Color(0.42f, 1f, 0.68f);
                }
                else if (_session.Quests.IsQuestActive(view.QuestId))
                {
                    view.Status.text = "进行中";
                    statusColor = new Color(1f, 0.83f, 0.34f);
                }
                else if (_session.Quests.HasBeenStarted(view.QuestId))
                {
                    view.Status.text = "可重试";
                    statusColor = new Color(0.88f, 0.72f, 1f);
                }
                else
                {
                    view.Status.text = "待触发";
                    statusColor = ThreeBodyUiPalette.AccentSoft;
                }

                view.Status.color = statusColor;
                view.Background.color = unlocked ? ThreeBodyUiPalette.PanelSoft : new Color(0.08f, 0.10f, 0.16f, 0.88f);
                view.Cover.color = unlocked ? Color.white : new Color(0.42f, 0.44f, 0.50f, 0.48f);
                view.Outline.effectColor = unlocked
                    ? new Color(0.38f, 0.72f, 1f, 0.48f)
                    : new Color(0.36f, 0.38f, 0.46f, 0.42f);
            }
        }

        private Sprite LoadStorySprite(string path)
        {
            if (_storylineSprites.TryGetValue(path, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
                sprite = Resources.LoadAll<Sprite>(path).FirstOrDefault();
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f));
            }

            if (sprite != null)
                _storylineSprites[path] = sprite;
            return sprite;
        }

        private void RefreshCaptainCards()
        {
            if (_captainContent == null)
                return;

            for (var i = _captainContent.transform.childCount - 1; i >= 0; i--)
                Destroy(_captainContent.transform.GetChild(i).gameObject);

            foreach (var captain in CaptainService.Definitions)
                CreateCaptainCard(captain);
        }

        private void CreateCaptainCard(CaptainDefinition captain)
        {
            var card = new GameObject(captain.Id + "Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            card.transform.SetParent(_captainContent.transform, false);
            card.GetComponent<LayoutElement>().preferredHeight = 188f;
            var selected = _captains.Selected == captain.Id;
            card.GetComponent<Image>().color = selected
                ? ThreeBodyUiPalette.PanelSelected
                : ThreeBodyUiPalette.PanelSoft;
            card.GetComponent<Button>().onClick.AddListener(() =>
            {
                _captains.Select(captain.Id);
                RefreshCaptainCards();
            });

            var portraitFrame = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portraitFrame.transform.SetParent(card.transform, false);
            var portraitRect = portraitFrame.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.02f, 0.08f);
            portraitRect.anchorMax = new Vector2(0.19f, 0.92f);
            portraitRect.offsetMin = portraitRect.offsetMax = Vector2.zero;
            var portrait = portraitFrame.GetComponent<Image>();
            portrait.color = ThreeBodyUiPalette.Panel;
            var sprite = GetCaptainPortrait(captain.PortraitPath);
            if (sprite != null)
            {
                portrait.sprite = sprite;
                portrait.preserveAspect = true;
                portrait.color = Color.white;
            }
            else
            {
                var missing = NewCaptainText(portraitFrame.transform, "Missing", "头像\n待导入", 16, TextAnchor.MiddleCenter);
                missing.rectTransform.anchorMin = Vector2.zero;
                missing.rectTransform.anchorMax = Vector2.one;
                missing.rectTransform.offsetMin = missing.rectTransform.offsetMax = Vector2.zero;
                missing.color = ThreeBodyUiPalette.TextMuted;
            }

            var name = NewCaptainText(card.transform, "Name", captain.Name, 28, TextAnchor.MiddleLeft);
            name.rectTransform.anchorMin = new Vector2(0.22f, 0.61f);
            name.rectTransform.anchorMax = new Vector2(0.66f, 0.91f);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
            name.color = Color.white;

            var state = NewCaptainText(card.transform, "State", selected ? "已选择" : "点击选择", 18, TextAnchor.MiddleRight);
            state.rectTransform.anchorMin = new Vector2(0.7f, 0.64f);
            state.rectTransform.anchorMax = new Vector2(0.96f, 0.9f);
            state.rectTransform.offsetMin = state.rectTransform.offsetMax = Vector2.zero;
            state.color = selected ? new Color(0.55f, 1f, 0.72f) : ThreeBodyUiPalette.TextMuted;

            var skill = NewCaptainText(card.transform, "Skill", "技能：" + captain.SkillName, 21, TextAnchor.MiddleLeft);
            skill.rectTransform.anchorMin = new Vector2(0.22f, 0.38f);
            skill.rectTransform.anchorMax = new Vector2(0.96f, 0.62f);
            skill.rectTransform.offsetMin = skill.rectTransform.offsetMax = Vector2.zero;
            skill.color = ThreeBodyUiPalette.Accent;

            var description = NewCaptainText(card.transform, "Description", captain.Description, 17, TextAnchor.UpperLeft);
            description.rectTransform.anchorMin = new Vector2(0.22f, 0.08f);
            description.rectTransform.anchorMax = new Vector2(0.96f, 0.39f);
            description.rectTransform.offsetMin = description.rectTransform.offsetMax = Vector2.zero;
            description.color = new Color(0.90f, 0.86f, 0.96f);
        }

        private Sprite GetCaptainPortrait(string path)
        {
            if (_captainPortraits.TryGetValue(path, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            if (sprite != null)
                _captainPortraits[path] = sprite;
            return sprite;
        }

        private static Text NewCaptainText(Transform parent, string name, string value, int size, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void ToggleRelationsPanel()
        {
            if (_relationsPanel != null)
            {
                _relationsPanel.SetActive(!_relationsPanel.activeSelf);
                return;
            }

            _relationsPanel = new GameObject("Preview5RelationsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = _relationsPanel.GetComponent<RectTransform>();
            rect.SetParent(transform.root, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 520f);
            _relationsPanel.GetComponent<Image>().color = ThreeBodyUiPalette.PanelDeep;
            _relationsPanel.transform.SetAsLastSibling();

            var layout = _relationsPanel.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = new Vector2(8f, 4f);
            layout.cellSize = new Vector2(350f, 28f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            var title = NewRelationText(rect, "玩家势力关系", 26);
            title.color = ThreeBodyUiPalette.AccentSoft;
            foreach (var faction in _database.FactionList.OrderBy(item => item.Id.Value))
            {
                var reputation = GetFactionReputation(faction);
                var state = reputation > 25 ? "友好" : reputation < -25 ? "敌对" : "中立";
                var row = NewRelationText(rect,
                    $"{faction.Id.Value:00}  {_localization.GetString(faction.Name)}    {reputation:+0;-0;0}  {state}", 18);
                row.color = reputation > 25
                    ? ThreeBodyUiPalette.Accent
                    : reputation < -25
                        ? new Color(1f, 0.35f, 0.25f)
                        : new Color(0.85f, 0.85f, 0.65f);
            }

        }

        private int GetFactionReputation(GameDatabase.DataModel.Faction faction)
        {
            foreach (var regionId in _session.Regions.Regions)
            {
                var region = _regionMap[regionId];
                if (region != GameModel.Region.Empty && region.Faction.Id == faction.Id)
                    return _session.Quests.GetFactionRelations(region.HomeStar);
            }

            if (faction.Id.Value == GameModel.Region.TrisolarisFactionId) return -50;
            return faction.Id.Value >= GameModel.Region.StarshipEarthFactionId ? 50 : -50;
        }

        private static Text NewRelationText(Transform parent, string value, int size)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 28f;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = value;
            return text;
        }

        private void ApplyPreview4FactionIcon()
        {
            var icon = Resources.Load<Sprite>("Textures/UI/faction_relations_preview4");
            if (icon == null)
                return;

            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                var opensFactionPanel = false;
                for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    opensFactionPanel |= button.onClick.GetPersistentMethodName(i) == nameof(ShowFaction);

                if (!opensFactionPanel)
                    continue;

                var images = button.GetComponentsInChildren<Image>(true);
                var target = images.FirstOrDefault(image => image.gameObject != button.gameObject) ??
                             images.FirstOrDefault();
                if (target != null)
                {
                    target.sprite = icon;
                    target.preserveAspect = true;
                }
            }
        }

        private void OnPlayerPositionChanged(int starId)
        {
            InitButtons();
        }

        private void OnMapStateChanged(ViewMode view)
        {
            InitButtons();
        }

        private void InitButtons()
        {
            CreateCaptainButton();
            CreateStorylineButton();
            var view = _motherShip.ViewMode;

            StarViewButton.gameObject.SetActive(view == ViewMode.StarMap);
            GalaxyViewButton.gameObject.SetActive(view == ViewMode.StarSystem || view == ViewMode.GalaxyMap);
            GalaxyButtonsGroup.SetActive(view == ViewMode.StarMap);
            FiltersGroup.gameObject.SetActive(view == ViewMode.GalaxyMap);
            ShowInformation();
        }

        private void OnArrivedToObject(Galaxy.StarObjectType objectType)
        {
            switch (objectType)
            {
                case Galaxy.StarObjectType.Undefined:
                    ShowInformation();
                    break;
                case Galaxy.StarObjectType.Boss:
                    ShowBoss();
                    break;
                case Galaxy.StarObjectType.StarBase:
                    ShowFaction();
                    break;
                case Galaxy.StarObjectType.Wormhole:
                    ShowWormhole();
                    break;
                case Galaxy.StarObjectType.Military:
                    ShowMilitaryBase();
                    break;
                case Galaxy.StarObjectType.Challenge:
                    ShowChallenge();
                    break;
                case Galaxy.StarObjectType.Arena:
                    ShowArena();
                    break;
                case Galaxy.StarObjectType.Ruins:
                    ShowRuins();
                    break;
                case Galaxy.StarObjectType.Xmas:
                    ShowXmas();
                    break;
                case Galaxy.StarObjectType.Survival:
                    ShowSurvival();
                    break;
                case Galaxy.StarObjectType.BlackMarket:
                    ShowBlackMarket();
                    break;
                case Galaxy.StarObjectType.Hive:
                    ShowPandemic();
                    break;
                case Galaxy.StarObjectType.Event:
                    _motherShip.CurrentStar.LocalEvent.Start();
                    break;
            }
        }

        private void OnArrivedToPlanet(int planetId)
        {
            ShowPlanet(planetId);
        }

        private GameObject _relationsPanel;
        private GameObject _captainPanel;
        private GameObject _captainContent;
        private GameObject _storylinePanel;
        private readonly Dictionary<string, Sprite> _captainPortraits = new();
        private readonly Dictionary<string, Sprite> _storylineSprites = new();
        private readonly Dictionary<int, StorylineCardView> _storylineCards = new();

        private sealed class StorylineCardView
        {
            public int QuestId;
            public bool RequiresRoot;
            public Image Background;
            public Image Cover;
            public Text Status;
            public Outline Outline;
        }
    }
}
