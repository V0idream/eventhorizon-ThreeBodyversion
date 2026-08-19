using Combat.Scene;

namespace Combat.Ai
{
	public class WaitAction : IAction
	{
		public void Perform(Context context, ShipControls controls)
		{
			controls.Course = RotationHelpers.Angle(BattlefieldGeometry.Delta(
				context.Ship.Body.WorldPosition(), context.Enemy.Body.WorldPosition()));
		}
	}
}
