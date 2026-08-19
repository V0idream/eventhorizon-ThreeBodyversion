using Combat.Scene;

namespace Combat.Ai.BehaviorTree.Nodes
{
	public class LookAtTargetNode : INode
	{
		public NodeState Evaluate(Context context)
		{
			if (context.TargetShip == null)
				return NodeState.Failure;

			var direction = BattlefieldGeometry.Delta(context.Ship.Body.WorldPosition(), context.TargetShip.Body.WorldPosition());
			context.Controls.Course = RotationHelpers.Angle(direction);
			return NodeState.Running;
		}
	}
}
