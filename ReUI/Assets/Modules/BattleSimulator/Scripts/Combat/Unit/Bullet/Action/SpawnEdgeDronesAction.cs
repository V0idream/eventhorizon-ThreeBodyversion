using Combat.Collision;
using Combat.Component.Ship;
using Combat.Factory;
using Combat.Unit;
using Constructor;

namespace Combat.Component.Bullet.Action
{
    public sealed class SpawnEdgeDronesAction : IAction
    {
        public SpawnEdgeDronesAction(IBullet bullet, IShip owner, int buildId, int count,
            float predatorDamage = EdgeDroneRuntime.DefaultPredatorDamage)
        {
            _bullet = bullet;
            _owner = owner;
            _buildId = buildId;
            _count = count;
            _predatorDamage = predatorDamage;
        }
        public ConditionType Condition => ConditionType.OnDetonate | ConditionType.OnExpire;
        public CollisionEffect Invoke()
        {
            if (_used || _owner == null || !_owner.IsActive()) return CollisionEffect.None;
            _used = true;
            EdgeDroneRuntime.QueueSpawn(_buildId, _owner, _bullet.Body.WorldPosition(), _count,
                DroneBehaviour.Aggressive, _predatorDamage);
            return CollisionEffect.None;
        }
        public void Dispose() { }
        private readonly IBullet _bullet;
        private readonly IShip _owner;
        private readonly int _buildId;
        private readonly int _count;
        private readonly float _predatorDamage;
        private bool _used;
    }
}
