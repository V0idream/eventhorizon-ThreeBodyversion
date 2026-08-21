using System.Linq;
using Combat.Component.Ship;
using Combat.Domain;
using Combat.Manager;
using Combat.Scene;
using Combat.Unit;
using Game.Adventure;
using GameStateMachine.States;
using Services.Gui;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Gui.Combat
{
    public class ShipSelectionPanel : MonoBehaviour
    {
        [Inject] private readonly CombatManager _manager;
        [Inject] private readonly IScene _scene;
        [Inject] private readonly AdventureRun _adventureRun;
        [Inject] private readonly AdventureEditShipSignal.Trigger _adventureEditShipTrigger;

        [SerializeField] private ShipList _enemyShips;
        [SerializeField] private ShipList _playerShips;

        // CombatManager must not resume its automatic replacement-ship flow while
        // the player is deciding which ready ship to deploy.
        public bool IsOpen => Window.IsVisible;

        public void Open(ICombatModel combatModel)
        {
            if (Window.IsVisible)
                return;

            EnsureAdventureModifyButton();
            if (_adventureModifyButton != null)
                _adventureModifyButton.gameObject.SetActive(_adventureRun.Active);

            _enemyShips.Initialize(combatModel.EnemyFleet, combatModel.EnemyFleet.Ships.FindIndex(item => item.Status == ShipStatus.Active));
            _playerShips.Initialize(combatModel.PlayerFleet, combatModel.PlayerFleet.Ships.FindIndex(item => item.Status == ShipStatus.Active));
            GetComponent<IWindow>().Open();
        }

        public void StartButtonClicked()
        {
            var ship = _playerShips.SelectedShip;
            if (ship.Status != ShipStatus.Ready)
                return;

            _manager.CreateShip(ship);
            GetComponent<IWindow>().Close();
        }

        public void AdventureModifyButtonClicked()
        {
            if (!_adventureRun.Active) return;
            var selected = _playerShips.SelectedShip;
            if (selected == null || selected.Status == ShipStatus.Destroyed || selected.ShipData == null)
                return;

            GetComponent<IWindow>().Close();
            _adventureEditShipTrigger.Fire(selected.ShipData);
        }

        private void EnsureAdventureModifyButton()
        {
            if (_adventureModifyButton != null) return;

            var existing = transform.Find("AdventureModifyButton");
            if (existing != null)
            {
                _adventureModifyButton = existing.GetComponent<Button>();
                return;
            }

            var panel = transform.Find("Panel");
            if (panel == null) return;
            var template = panel.Find("Button")?.GetComponent<Button>();
            if (template == null) return;

            // Keep the authored horizontal layout untouched.  The original
            // start button lives inside Panel; this Adventure-only action is
            // overlaid directly on the full-screen selection root instead.
            var clone = Instantiate(template.gameObject, transform, false);
            clone.name = "AdventureModifyButton";
            _adventureModifyButton = clone.GetComponent<Button>();
            _adventureModifyButton.onClick = new Button.ButtonClickedEvent();
            _adventureModifyButton.onClick.AddListener(AdventureModifyButtonClicked);

            var rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.76f, 0.045f);
            rect.anchorMax = new Vector2(0.94f, 0.14f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // The authored start button is icon-only. Remove that inherited
            // battle glyph and add a clear textual action for Adventure mode.
            var inheritedIcon = clone.transform.Find("Image")?.GetComponent<Image>();
            if (inheritedIcon != null)
                inheritedIcon.enabled = false;
            var generatedIcon = clone.transform.Find("ReUI Icon Host");
            if (generatedIcon != null)
                Destroy(generatedIcon.gameObject);

            var sceneFont = GetComponentsInChildren<Text>(true)
                .FirstOrDefault(item => item != null && item.font != null)?.font;
            var labelObject = new GameObject("AdventureModifyLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.layer = clone.layer;
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
            var label = labelObject.GetComponent<Text>();
            label.font = sceneFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "改装";
            label.fontSize = 28;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 28;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            clone.SetActive(false);
        }

        private void Update()
        {
            if (_scene.PlayerShip.IsActive() && Window.IsVisible)
                Window.Close();
        }

        private IWindow Window { get { return GetComponent<IWindow>(); } }
		private Button _adventureModifyButton;
    }
}
