using Combat.Component.Ship;
using Combat.Component.Unit.Classification;
using Combat.Scene;
using Combat.Unit;
using UnityEngine;

namespace Combat.Ai
{
	public class Computer : IController
	{
		public Computer(IScene scene, IShip ship, int level, bool autopilotMode, bool forceEngagement)
		{
			_ship = ship;
			_level = level;
			_scene = scene;
			_autopilotMode = autopilotMode;
			_forceEngagement = forceEngagement;
			_attackRange = Helpers.ShipMaxRange(_ship);
			_targets = new TargetList(_scene);
			_threats = new ThreatList(_scene);
			_fallbackAttack = new CommonWeaponsAttackAction(_ship, false, false);
		}

		public ControllerStatus Status
		{
            get
            {
				if (!_ship.IsActive()) return ControllerStatus.Dead;
				if (_autopilotMode && _autoPilotCooldown > 0) return ControllerStatus.Idle;
				return ControllerStatus.Active;
			}
		}

		public void Update(float deltaTime, in AiManager.Options options)
		{
			if (_autopilotMode)
			{
				if (_ship.Controls.DataChanged)
				{
					_ship.Controls.DataChanged = false;
					_autoPilotCooldown = AutoPilotDelay;
				}

				if (_autoPilotCooldown > 0)
				{
					_autoPilotCooldown -= deltaTime;
					return;
				}
			}

			var enemy = GetEnemy();
			// Adventure waves behave like exploration encounters: newly spawned
			// hostile ships should immediately use the lightweight pursuit combat
			// loop instead of waiting for campaign behavior-tree strategy state.
			// The previous fallback still allowed StrategySelector to own the
			// decision and could therefore leave a valid ship idle.
			if (_forceEngagement && enemy != null && enemy.IsActive())
			{
				ApplyForcedEngagement(enemy, deltaTime);
				return;
			}

			var strategy = GetStrategy();
			if (strategy == null)
			{
				if (_forceEngagement && enemy != null && enemy.IsActive())
					ApplyForcedEngagement(enemy, deltaTime);
				else
					Stop();
				return;
			}

			_threats.Update(deltaTime, _ship, strategy);
		    _targets.Update(deltaTime, _ship, enemy);
			var context = new Context(_ship, enemy, _targets, _threats, _currentTime);

			strategy.Apply(context, _controls);
			_controls.Apply(_ship);

			_currentTime += deltaTime;
			_enemyUpdateCooldown -= deltaTime;
			_strategyUpdateCooldown -= deltaTime;

			if (_autopilotMode)
				_ship.Controls.DataChanged = false;
		}

		private void ApplyForcedEngagement(IShip enemy, float deltaTime)
		{
			var context = new Context(_ship, enemy, _targets, _threats, _currentTime);
			_targets.Update(deltaTime, _ship, enemy);
			new FollowAction(Mathf.Max(10f, _attackRange * 0.65f)).Perform(context, _controls);
			_fallbackAttack.Perform(context, _controls);
			_controls.Apply(_ship);

			_currentTime += deltaTime;
			_enemyUpdateCooldown -= deltaTime;
			_strategyUpdateCooldown -= deltaTime;
		}

		private void Stop()
		{
			_ship.Controls.Throttle = 0;
			_ship.Controls.Course = null;
			_ship.Controls.Systems.Clear();
		}

		private IStrategy GetStrategy()
		{
		    if (!_enemy.IsActive())
		        return null;//_strategy = _ship.Type.Side == UnitSide.Player ? new CollectLoot() : null;
		    if (_level < 0)
		        return null;

			if (_strategy != null && _strategyUpdateCooldown > 0)
				return _strategy;

			_strategy = StrategySelector./*BestAvailable*/Random(_ship, _enemy, _level, new System.Random(), _scene);
			_strategyUpdateCooldown = StrategyUpdateInterval;

			//UnityEngine.Debug.Log("Strategy: " + _strategy.GetType().Name);
			return _strategy;
		}

		private IShip GetEnemy()
		{
			if (_enemy.IsActive() && CombatRelations.AreEnemies(_ship.Type, _enemy.Type) &&
				_enemyUpdateCooldown > 0)
				return _enemy;

			_enemyUpdateCooldown = EnemyUpdateInterval;

			var newEnemy = _scene.Ships.GetEnemyForMissile(_ship, 0, _attackRange, 360, true, true);

			// Adventure mode can create ships dynamically after the normal combat
			// initialization path. If the missile target resolver rejects the new
			// target because of range/priority rules, AI must still acquire a valid
			// combat target instead of remaining idle forever.
			if (newEnemy == null)
			{
				var ships = _scene.Ships.Items;
				for (var i = 0; i < ships.Count; i++)
				{
					var candidate = ships[i];
					if (candidate != _ship && candidate.IsActive() &&
						CombatRelations.AreEnemies(_ship.Type, candidate.Type))
					{
						newEnemy = candidate;
						break;
					}
				}
			}
			if (newEnemy != _enemy)
				_strategy = null;

			return _enemy = newEnemy;
		}

		private IShip _enemy;
		private float _enemyUpdateCooldown;
		private float _strategyUpdateCooldown;
		private float _currentTime;
		private float _autoPilotCooldown = AutoPilotDelay;
		private IStrategy _strategy;
		private readonly bool _autopilotMode;
		private readonly bool _forceEngagement;
		private readonly ShipControls _controls = new();
		private readonly CommonWeaponsAttackAction _fallbackAttack;
	    private readonly ThreatList _threats;
	    private readonly TargetList _targets;
        private readonly float _attackRange;
		private readonly int _level;
 		private readonly IShip _ship;
	    private readonly IScene _scene;
		private const float EnemyUpdateInterval = 5.0f;
		private const float StrategyUpdateInterval = 10.0f;
	    private const float AutoPilotDelay = 2.0f;

        public class Factory : IControllerFactory
        {
			public Factory(IScene scene, int level, bool autopilotMode = false, bool forceEngagement = false)
			{
				_scene = scene;
				_level = level;
				_autopilotMode = autopilotMode;
				_forceEngagement = forceEngagement;
			}

			public IController Create(IShip ship)
            {
				return new Computer(_scene, ship, _level, _autopilotMode, _forceEngagement);
			}

			private readonly bool _forceEngagement;
			private readonly bool _autopilotMode;
			private readonly int _level;
			private readonly IScene _scene;
        }
	}
}
