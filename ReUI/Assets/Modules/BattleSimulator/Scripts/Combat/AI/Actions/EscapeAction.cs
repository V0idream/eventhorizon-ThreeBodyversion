using UnityEngine;
using Combat.Scene;

namespace Combat.Ai
{
	public class EscapeAction : IAction
	{
		public EscapeAction(float distance)
		{
			_distanceMin = distance;
		}
		
		public void Perform(Context context, ShipControls controls)
		{
			var ship = context.Ship;
			var enemy = context.Enemy;

			if (controls.MovementLocked)
				return;
			
			var currentDir = BattlefieldGeometry.Delta(enemy.Body.WorldPosition(), ship.Body.WorldPosition()).normalized;
			var distance = BattlefieldGeometry.Distance(ship.Body.WorldPosition(), enemy.Body.WorldPosition());
			
			if (distance >= _distanceMin)
				return;

			var enemyDirection = enemy.Body.Velocity.magnitude > 0.1f ? enemy.Body.Velocity : RotationHelpers.Direction(enemy.Body.Rotation);
			var normal = Vector3.Cross(currentDir,enemyDirection).normalized;
			var targetDir = (Vector2)Vector3.Cross(enemyDirection, normal).normalized*ship.Engine.MaxVelocity - enemy.Body.Velocity;
			
			var targetAngle = RotationHelpers.Angle(targetDir);			
			controls.Course = targetAngle;
			controls.Thrust = 1;
		}
		
		private readonly float _distanceMin;
	}
}
