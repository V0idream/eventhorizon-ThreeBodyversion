using System.Collections.Generic;
using Combat.Collision.Manager;
using Combat.Component.Body;
using Combat.Component.Bullet;
using Combat.Component.Ship;
using Combat.Component.Ship.Effects;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using Combat.Factory;
using Combat.Unit;
using GameDatabase.Enums;

namespace Combat.Collision.Behaviour.Action
{
    public sealed class NanoTorpedoAction : ICollisionAction
    {
        public void Invoke(IUnit self, IUnit target, CollisionData collisionData, ref Impact selfImpact, ref Impact targetImpact)
        {
            if (_used || !collisionData.IsNew || target is not IShip ship) return;
            _used = true;
            var resolved = targetImpact;
            resolved.ApplyShield(ship.Stats.Shield.Value, ship.Stats.Resistance.ShieldCorrosive);
            var damage = resolved.GetTotalDamage(ship.Stats.Resistance);
            var count = UnityEngine.Mathf.FloorToInt(UnityEngine.Mathf.Max(0f, damage) / 1000f);
            var owner = self.Type.Owner;
            if (count > 0 && owner != null)
                EdgeDroneRuntime.QueueSpawn(EdgeDroneRuntime.NormalBuildId, owner, target.Body.WorldPosition(), count);
        }
        public void Dispose() { }
        private bool _used;
    }

    public sealed class TemporaryConversionAction : ICollisionAction
    {
        public void Invoke(IUnit self, IUnit target, CollisionData collisionData, ref Impact selfImpact, ref Impact targetImpact)
        {
            if (_used || !collisionData.IsNew || target is not IShip ship || self.Type.Owner == null) return;
            _used = true;
            if (self.Type.Owner.Type.Side == UnitSide.Enemy && ship.Type.Side == UnitSide.Player) return;
            if (ship.Features.ImmuneToEffects) return;
            ship.AddEffect(new TemporaryConversionEffect(ship, self.Type.Owner.Type, 10f));
        }
        public void Dispose() { }
        private bool _used;
    }

    public sealed class StasisAction : ICollisionAction
    {
        public void Invoke(IUnit self, IUnit target, CollisionData collisionData, ref Impact selfImpact, ref Impact targetImpact)
        {
            if (!collisionData.IsNew) return;
            if (target is IShip ship)
            {
                if (!ship.Features.ImmuneToEffects) ship.AddEffect(new StasisEffect(1.25f));
            }
            else if (target is IBullet bullet)
            {
                ProjectileStasisStatus.Apply(bullet, 1.25f);
            }
        }
        public void Dispose() { }
    }

    public static class ProjectileStasisStatus
    {
        public static void Apply(IBullet bullet, float duration)
        {
            if (bullet == null) return;
            Remaining[bullet] = UnityEngine.Mathf.Max(Remaining.TryGetValue(bullet, out var old) ? old : 0f, duration);
        }

        public static bool Update(IBullet bullet, float elapsedTime)
        {
            if (!Remaining.TryGetValue(bullet, out var remaining)) return false;
            remaining -= elapsedTime;
            if (remaining <= 0f) { Remaining.Remove(bullet); return false; }
            Remaining[bullet] = remaining;
            return true;
        }
        private static readonly Dictionary<IBullet, float> Remaining = new();
    }

    public static class VirusCodeTargeting
    {
        public static void Restrict(IBody body) { if (body != null) Restricted.Add(body); }
        public static bool IsBlocked(IBody body, IUnit target) =>
            body != null && Restricted.Contains(body) && target?.Type?.Side == UnitSide.Player;
        private static readonly HashSet<IBody> Restricted = new();
    }
}
