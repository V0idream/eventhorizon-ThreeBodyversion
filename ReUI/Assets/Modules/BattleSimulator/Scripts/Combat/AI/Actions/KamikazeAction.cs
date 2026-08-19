using UnityEngine;
using Combat.Scene;

namespace Combat.Ai
{
	public class KamikazeAction : IAction
	{
		public KamikazeAction(int deviceId)
		{
			_deviceId = deviceId;
			_time = 1.0f; // TODO
		}
		
		public void Perform(Context context, ShipControls controls)
		{
			var ship = context.Ship;
			var enemy = context.Enemy;
			if (enemy.Stats != null && ship.Stats.Armor.Value > enemy.Stats.Armor.Value + enemy.Stats.Shield.Value)
				return;

			var shipPosition = ship.Body.WorldPosition() + ship.Body.WorldVelocity() * _time;
			var enemyPosition = enemy.Body.WorldPosition() + enemy.Body.WorldVelocity() * _time;
			if (BattlefieldGeometry.Distance(shipPosition, enemyPosition) <= ship.Body.Scale/2 + enemy.Body.Scale/2)
			{
				controls.ActivateSystem(_deviceId);
				controls.Thrust = 0f;
			}
			else
			{
				Vector2 target;
				float timeInterval;

				var nearestEnemyPosition = BattlefieldGeometry.NearestEquivalent(ship.Body.WorldPosition(), enemy.Body.WorldPosition());
				if (!Geometry.GetTargetPosition(
					nearestEnemyPosition,
					enemy.Body.Velocity,
					ship.Body.WorldPosition(),
					ship.Engine.MaxVelocity,
					out target,
					out timeInterval))
				{
					return;
				}

				var direction = BattlefieldGeometry.Delta(ship.Body.WorldPosition(), target);
				var course = RotationHelpers.Angle(direction);
				controls.Course = course;

				if (Mathf.Abs(Mathf.DeltaAngle(ship.Body.Rotation, course)) < 30)
				{
					controls.Thrust = 1.0f;
				}
			}
		}
		
		private readonly int _deviceId;
		private readonly float _time;
	}
}
