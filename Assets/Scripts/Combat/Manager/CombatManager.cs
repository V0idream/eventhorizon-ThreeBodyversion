using UnityEngine;
using System.Linq;
using Combat.Ai;
using Combat.Component.Ship;
using Combat.Component.Triggers;
using Combat.Component.Unit.Classification;
using Combat.Domain;
using Combat.Factory;
using Combat.Scene;
using Combat.Unit;
using Combat.Unit.Ship.Effects.Special;
using GameStateMachine.States;
using Services.Audio;
using Services.Messenger;
using GameDatabase;
using Gui.Combat;
using Maths;
using Zenject;
using GameDatabase.Enums;
using GameDatabase.Extensions;
using Combat.Ai.Calculations;
using Combat.Component.Platform;
using Combat.Component.Systems.Weapons;
using GameServices.Player;

namespace Combat.Manager
{
    public class CombatManager : IInitializable, ITickable
    {
        public enum AllyOrder
        {
            Free,
            Attack,
            Defend,
        }

        [Inject]
        private CombatManager(
            IMessenger messenger,
            ISoundPlayer soundPlayer,
            ExitSignal.Trigger exitTrigger,
            CombatRetreatSignal.Trigger combatRetreatTrigger)
        {
            _soundPlayer = soundPlayer;
            _exitTrigger = exitTrigger;
            _combatRetreatTrigger = combatRetreatTrigger;
            _messenger = messenger;

            _messenger.AddListener(EventType.EscapeKeyPressed, OnEscapeKeyPressed);
            _messenger.AddListener<IShip>(EventType.CombatShipCreated, OnShipCreated);
            _messenger.AddListener<IShip>(EventType.CombatShipDestroyed, OnShipDestroyed);
        }

        [Inject] private readonly IScene _scene;
        [Inject] private readonly IDatabase _database;
        [Inject] private readonly ShipControlsPanel _shipControlsPanel;
        [Inject] private readonly ShipFactory _shipFactory;
        [Inject] private readonly ControllerFactory _controllerFactory;
        [Inject] private readonly SpaceObjectFactory _spaceObjectFactory;
        [Inject] private readonly EffectFactory _effectFactory;

        [Inject] private readonly ShipSelectionPanel _shipSelectionPanel;
        [Inject] private readonly ShipStatsPanel _playerStatsPanel;
        [Inject] private readonly ShipStatsPanel _enemyStatsPanel;
        [Inject] private readonly CombatMenu _combatMenu;
        [Inject] private readonly Settings _settings;
        [Inject] private readonly RadarPanel _radarPanel;
        [Inject] private readonly ICombatModel _combatModel;

        public void Initialize()
        {
            UnityEngine.Debug.Log("OnCombatStarted");

            var random = new System.Random();

            var level = _database.GalaxySettings.EnemyLevel(_combatModel.Rules.StarLevel);
            var powerMultiplier = Experience.LevelToPowerMultiplier(level);

            if (!_combatModel.Rules.DisableAsteroids)
            {
                for (int i = 0; i < 10; ++i)
                {
                    var size = Random.Range(2f, 5f);
                    var position = _scene.FindFreePlace(20f, UnitSide.Undefined);

                    var weight = size * size * 5f;
                    var hitPoints = size * size * 100 * powerMultiplier;
                    var damageMultiplier = powerMultiplier;

                    var velocity = Random.insideUnitCircle * 10 / size;
                    _spaceObjectFactory.CreateAsteroid(position, velocity, size, weight, hitPoints, damageMultiplier);
                }
            }

            if (!_combatModel.Rules.DisablePlanet)
            {
                var r = random.NextFloat();
                var g = Mathf.Sqrt(1f - r * r);
                var b = random.NextFloat();
                var color = Color.Lerp(new Color(r, g, b), Color.gray, 0.5f);

                var position = new Vector2(_scene.Settings.AreaWidth * random.NextFloat(), _scene.Settings.AreaHeight * random.NextFloat());
                var size = 30 + random.NextFloat() * 10;
                _spaceObjectFactory.CreatePlanet(position, size, color);
            }

            // The original implementation relied on an update-tick fallback to
            // create the first enemy. That leaves the battlefield empty during
            // scene startup and lets the counter prefab display its placeholder
            // value. Create the complete initial wave here instead.
            var initialEnemies = Mathf.Max(1, _combatModel.Rules.InitialEnemyShips);
            var enemiesToCreate = Mathf.Max(0, initialEnemies - ActiveEnemyCount());
            foreach (var ship in _combatModel.EnemyFleet.Ships
                         .Where(item => item.Status == ShipStatus.Ready)
                         .Take(enemiesToCreate)
                         .ToArray())
                CreateShip(ship);

            foreach (var ally in _combatModel.AllyFleet.Ships
                         .Where(item => !item.IsCollaborativeAlly)
                         .Where(item => item.Status == ShipStatus.Ready)
                         .ToArray())
                CreateShip(ally);

            UpdateEnemyCounter(true);
            CheckIfCanCallNextEnemy();
        }

        public void OnShipCreated(IShip ship)
        {
            if (ship.Type.Class != UnitClass.Ship)
                return;

            CheckIfCanCallNextEnemy();

            switch (ship.Type.Side)
            {
                case UnitSide.Player:
					_manualShipChangePending = false;
                    _shipControlsPanel.Load(ship);
                    _messenger.Broadcast(EventType.PlayerShipCountChanged,
                        _combatModel.PlayerFleet.Ships.Count(item => item.Status == ShipStatus.Ready));
                    break;
                case UnitSide.Enemy:
                    _radarPanel.Add(ship);
                    // Factory events are raised before ShipInfo stores the new
                    // unit. Defer the authoritative status/count refresh.
                    _enemyCounterDirty = true;
                    break;
                case UnitSide.Ally:
                    _radarPanel.Add(ship);
                    break;
            }
        }

        public void OnShipDestroyed(IShip ship)
        {
            if (ship.Type.Class != UnitClass.Ship)
                return;

            if (ship.Type.Side == UnitSide.Enemy)
            {
                // ShipInfo is updated after the factory event is emitted, so
                // the authoritative count is synchronized on the next Tick.
                _enemyCounterDirty = true;
            }
			else if (ship.Type.Side == UnitSide.Ally)
			{
				var allyInfo = _combatModel.AllyFleet.GetInfo(ship);
				if (allyInfo != null && allyInfo.IsCollaborativeAlly && !ReferenceEquals(allyInfo, _collaboratorBeingTransferred))
				{
					// A carried ship destroyed while fighting as an ally must also
					// disappear from the player's reserve. Otherwise the takeover
					// path can recreate that already-destroyed hull.
					var reserve = _combatModel.PlayerFleet.Ships.FirstOrDefault(item =>
						ReferenceEquals(item.ShipData, allyInfo.ShipData));
					if (reserve != null && reserve.Status == ShipStatus.Ready)
						reserve.Destroy();
				}
			}

            CheckIfCanCallNextEnemy();
        }

        public void CreateShip(IShipInfo ship)
        {
            CreateShip(ship, _scene.FindFreePlace(40, ship.Side));
        }

        public void CycleAllyOrder()
        {
            AllyTactic = AllyTactic == AllyOrder.Free
                ? AllyOrder.Attack
                : AllyTactic == AllyOrder.Attack
                    ? AllyOrder.Defend
                    : AllyOrder.Free;
        }

        public AllyOrder AllyTactic { get; private set; } = AllyOrder.Free;

        public string AllyOrderName => AllyTactic switch
        {
            AllyOrder.Attack => "攻击",
            AllyOrder.Defend => "防御",
            _ => "自由",
        };

        private void CreateShip(IShipInfo ship, Vector2 position)
        {
            if (ship == null)
                return;

            ship.Create(_shipFactory, position, ship.Side == UnitSide.Enemy
                ? _combatModel.EnemyFleet.AiLevel
                : ship.Side == UnitSide.Ally
                    ? _combatModel.AllyFleet.AiLevel
                    : _combatModel.PlayerFleet.AiLevel);
        }

        public bool IsGamePaused { get { return _pausedCount > 0; } }

        public void OnEscapeKeyPressed()
        {
            if (_combatMenu)
                _combatMenu.Open();
        }

        public void Surrender()
        {
            _combatModel.PlayerFleet.DestroyAllShips();
            Exit();
        }

        public void Retreat()
        {
            _combatRetreatTrigger.Fire();
        }

        public void Exit()
        {
            _exitTrigger.Fire();
			_scene.Clear();
        }

        public bool CanChangeShip()
        {
            if (_combatModel.Rules.ShipSelection != PlayerShipSelectionMode.Default) return false;
            return _combatModel.PlayerFleet.IsAnyShipLeft();
        }

        public bool CanKillAllEnemies => _combatModel.Rules.KillThemAllButton;

        public void ChangeShip()
        {
            var player = _scene.PlayerShip;
            if (!player.IsActive() || player.Effects.All.OfType<ShipRetreatEffect>().Any())
                return;

			_manualShipChangePending = true;
			_nextPlayerShipCooldown = 0f;
            var chargeEffect = new ShipRetreatingEffect(player, _effectFactory, ConditionType.OnActivate, ConditionType.OnDeactivate);
            var warpEffect = new ShipWarpEffect(player, _effectFactory, _soundPlayer, _settings.ShipWarpSound, ConditionType.OnDeactivate);
            var soundEffect = new SoundLoopEffect(_soundPlayer, _settings.ShipRetreatSound, ConditionType.OnActivate, ConditionType.OnDeactivate);
            // Changing the active ship is not a combat retreat.  Firing the retreat
            // signal here moves the star-map state while the combat ship-selection
            // flow is still running, which leaves the regular combat scene black.
            player.AddEffect(new ShipRetreatEffect(7.0f, null, soundEffect, warpEffect, chargeEffect));
        }

        public void KillAllEnemies()
        {
            _combatModel.EnemyFleet.DestroyAllShips();
        }

        public bool CanCallNextEnemy() { return _canCallNextEnemy; }

        public void CallNextEnemy()
        {
            if (!CanCallNextEnemy())
                return;

            var shipInfo = GetNextEnemy();
            if (shipInfo == null)
                return;

            CreateShip(shipInfo);
            _soundPlayer.Play(_settings.ReinforcementSound);
        }

        public bool TryCallNextEnemyAutomatically()
        {
            var activeEnemies = ActiveEnemyCount();
            if (activeEnemies >= AutoEnemyLimit || _reinforcementCooldown < AutoEnemySpawnCooldown)
                return false;

            var shipInfo = GetNextEnemy();
            if (shipInfo == null)
                return false;

            _reinforcementCooldown = 0;
            CreateShip(shipInfo);
            _soundPlayer.Play(_settings.ReinforcementSound);
            return true;
        }

        private void CheckIfCanCallNextEnemy()
        {
            _canCallNextEnemy = _combatModel.Rules.NextEnemyButton &&
                                _combatModel.EnemyFleet.AnyAvailableShip() != null;
        }

        public void Tick()
        {
            if (_combatModel == null)
                return;

            UpdateLocalGame();
        }

        private void UpdateLocalGame()
        {
            var player = _scene.PlayerShip;
            var enemy = _scene.EnemyShip;

            UpdateEnemyCounter();

            if (player.IsActive() && !IsGamePaused)
            {
                _hasActivatedPlayerShip = true;
                DeployCollaborativeAllies(player);
                ApplyAllyOrders(player);

                if (ActiveEnemyCount() == 0)
                {
                    var nextEnemy = GetNextEnemy();
                    if (nextEnemy != null)
                    {
                        _reinforcementCooldown = 0;
                        CreateShip(nextEnemy);
                        _soundPlayer.Play(_settings.ReinforcementSound);
                    }
                }

                if (GetNextEnemy() != null && ActiveEnemyCount() < AutoEnemyLimit)
                {
                    _reinforcementCooldown += Time.deltaTime;
                    TryCallNextEnemyAutomatically();
                }

                if (ActiveEnemyCount() == 0 && _combatModel.EnemyFleet.AnyAvailableShip() == null)
                {
                    _battleEndCooldown += Time.deltaTime;
                    if (_battleEndCooldown >= _nextShipMaxCooldown)
                    {
                        Exit();
                        return;
                    }
                }
                else
                {
                    _battleEndCooldown = 0;
                }
            }

            if (!player.IsActive())
            {
				if (!_manualShipChangePending && _hasActivatedPlayerShip && ThreeBodySkillState.CollaborativeCombatUnlocked && TakeControlOfLargestCollaborator())
                    return;

                _nextPlayerShipCooldown += Time.deltaTime;
                if (_nextPlayerShipCooldown > _nextShipMaxCooldown)
                {
                    _nextPlayerShipCooldown = 0;

                    if (IsPlayerDefeated())
                    {
                        UnityEngine.Debug.Log("No more ships");
                        Exit();
                    }
                    else if (_combatModel.Rules.ShipSelection.CanChooseShip())
                    {
						_manualShipChangePending = false;
                        _shipSelectionPanel.Open(_combatModel);
                    }
                    else
                    {
                        var shipInfo = _combatModel.PlayerFleet.AnyAvailableShip();
                        CreateShip(shipInfo);
                    }
                }
            }
            else if (player.IsActive() && enemy.IsActive() && !IsGamePaused &&
                     !TargetingHelpers.CantDetectTarget(player, enemy))
            {
                _playerStatsPanel.Open(player);
                _enemyStatsPanel.Open(enemy);
            }
            else if (player.IsActive() && !IsGamePaused)
            {
                _playerStatsPanel.Open(player);
                _enemyStatsPanel.Close();
            }
        }

        private void DeployCollaborativeAllies(IShip player)
        {
            if (!ThreeBodySkillState.CollaborativeCombatUnlocked)
                return;

            var playerInfo = _combatModel.PlayerFleet.GetInfo(player);
            if (playerInfo == null)
                return;

            foreach (var ally in _combatModel.AllyFleet.Ships
                         .Where(item => item.IsCollaborativeAlly && item.Status == ShipStatus.Ready)
                         .Where(item => !ReferenceEquals(item.ShipData, playerInfo.ShipData))
                         .ToArray())
                CreateShip(ally);
        }

        private bool TakeControlOfLargestCollaborator()
        {
            var candidates = _combatModel.PlayerFleet.Ships
                .Where(item => item.Status == ShipStatus.Ready)
				.Where(item =>
				{
					var ally = FindCollaborativeAlly(item.ShipData);
					return ally == null || ally.Status != ShipStatus.Destroyed;
				})
                .ToArray();
            if (candidates.Length == 0)
                return false;

            var largestClass = candidates.Max(item => (int)item.ShipData.Model.SizeClass);
            var largest = candidates
                .Where(item => (int)item.ShipData.Model.SizeClass == largestClass)
                .OrderBy(_ => UnityEngine.Random.value)
                .FirstOrDefault();
            if (largest == null)
                return false;

			var collaborator = FindCollaborativeAlly(largest.ShipData);

            var position = collaborator?.ShipUnit?.Body.WorldPosition() ?? _scene.FindFreePlace(40, UnitSide.Player);
            // The friendly version represents the same carried craft.  Remove
            // it before recreating the unit with the player's controller.
			_collaboratorBeingTransferred = collaborator;
			collaborator?.Destroy();
			_collaboratorBeingTransferred = null;
            CreateShip(largest, position);
            _nextPlayerShipCooldown = 0;
            return true;
        }

		private IShipInfo FindCollaborativeAlly(Constructor.Ships.IShip shipData)
		{
			return _combatModel.AllyFleet.Ships.FirstOrDefault(item =>
				item.IsCollaborativeAlly && ReferenceEquals(item.ShipData, shipData));
		}

        private void ApplyAllyOrders(IShip player)
        {
            if (AllyTactic == AllyOrder.Free || player == null || !player.IsActive())
                return;

            var target = AllyTactic == AllyOrder.Attack
                ? _scene.LockedEnemyShip
                : FindNearestEnemyTo(player);
            if (target == null || !target.IsActive() || !CombatRelations.AreEnemies(player.Type, target.Type))
                return;

            lock (_scene.Ships.LockObject)
            {
                foreach (var ally in _scene.Ships.Items)
                {
                    if (!ally.IsActive() || ally.Type.Side != UnitSide.Ally)
                        continue;

                    foreach (var weapon in ally.Systems.All.OfType<IWeapon>())
                    {
                        weapon.Platform.ActiveTarget = target;
                        if (weapon.Platform is IUnitTargetingPlatform unitTargetingPlatform)
                            unitTargetingPlatform.ActiveUnitTarget = target;
                    }
                }
            }
        }

        private IShip FindNearestEnemyTo(IShip player)
        {
            IShip nearest = null;
            var nearestDistance = float.PositiveInfinity;
            lock (_scene.Ships.LockObject)
            {
                foreach (var candidate in _scene.Ships.Items)
                {
                    if (!candidate.IsActive() || !CombatRelations.AreEnemies(player.Type, candidate.Type))
                        continue;

                    var distance = (candidate.Body.Position - player.Body.Position).sqrMagnitude;
                    if (distance >= nearestDistance)
                        continue;

                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private int ActiveEnemyCount()
        {
            return _combatModel.EnemyFleet.Ships.Count(item => item.Status == ShipStatus.Active);
        }

        public int ReinforcementTimeLeft
        {
            get
            {
                if (GetNextEnemy() == null)
                    return 0;

                return Mathf.Max(0, Mathf.CeilToInt(AutoEnemySpawnCooldown - _reinforcementCooldown));
            }
        }

        private int RemainingEnemyCount()
        {
            return _combatModel.EnemyFleet.Ships.Count(item => item.Status != ShipStatus.Destroyed);
        }

        public int RemainingAllyCount
		{
			get
			{
				if (_combatModel?.AllyFleet == null)
					return 0;

				var activePlayerData = _scene?.PlayerShip != null && _scene.PlayerShip.IsActive()
					? _combatModel.PlayerFleet.GetInfo(_scene.PlayerShip)?.ShipData
					: null;
				return _combatModel.AllyFleet.Ships.Count(item =>
					item.Status != ShipStatus.Destroyed &&
					(!item.IsCollaborativeAlly || !ReferenceEquals(item.ShipData, activePlayerData)));
			}
		}

        public bool HasAlliedParticipants => RemainingAllyCount > 0;

        private void UpdateEnemyCounter(bool force = false)
        {
            var count = RemainingEnemyCount();
            if (!force && !_enemyCounterDirty && count == _lastEnemyCount)
                return;

            _enemyCounterDirty = false;
            _lastEnemyCount = count;
            _messenger.Broadcast(EventType.EnemyShipCountChanged, count);
            CheckIfCanCallNextEnemy();
        }

        private IShipInfo GetNextEnemy()
        {
            return _combatModel.EnemyFleet.Ships.FirstOrDefault(item => item.Status == ShipStatus.Ready);
        }

        private bool IsPlayerDefeated()
        {
            if (_combatModel.Rules.ShipSelection == PlayerShipSelectionMode.OnlyOneShip && 
                _scene.PlayerShip != null && _scene.PlayerShip.State == UnitState.Destroyed)
                return true;

            if (!_combatModel.PlayerFleet.IsAnyShipLeft())
                return true;

            return false;
        }

        private bool _canCallNextEnemy;
        private bool _hasActivatedPlayerShip;
		private bool _manualShipChangePending;
		private IShipInfo _collaboratorBeingTransferred;

        private float _reinforcementCooldown;
        private float _nextPlayerShipCooldown = _nextShipMaxCooldown;
        private float _battleEndCooldown;
        private int _lastEnemyCount = -1;
        private bool _enemyCounterDirty = true;
        private const float _nextShipMaxCooldown = 3.0f;
        private const float AutoEnemySpawnCooldown = 20.0f;
        private const int AutoEnemyLimit = 25;

        private int _pausedCount;
        private readonly ISoundPlayer _soundPlayer;
        private readonly ExitSignal.Trigger _exitTrigger;
        private readonly CombatRetreatSignal.Trigger _combatRetreatTrigger;
        private readonly IMessenger _messenger;
    }
}
