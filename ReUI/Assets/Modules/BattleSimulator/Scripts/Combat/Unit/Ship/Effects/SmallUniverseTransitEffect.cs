using Combat.Component.Body;
using Combat.Component.Engine;
using Combat.Component.Features;
using Combat.Component.Ship;
using Combat.Component.Stats;
using Combat.Component.Systems;
using Combat.Component.Triggers;
using Combat.Scene;
using UnityEngine;

namespace Combat.Component.Ship.Effects
{
    /// <summary>
    /// Removes a ship from targeting, rendering and collision without removing
    /// it from the scene lists. This keeps its database/runtime identity intact
    /// while it is inside a small universe.
    /// </summary>
    public sealed class SmallUniverseTransitEffect : IShipEffect, IFeaturesModification,
        IEngineModification, ISystemsModification
    {
        public SmallUniverseTransitEffect(IShip ship, IScene scene, float duration,
            bool drainResources, bool safeReturn, bool grantExitInvulnerability)
        {
            _ship = ship;
            _scene = scene;
            _remaining = Mathf.Max(0.1f, duration);
            _drainResources = drainResources;
            _safeReturn = safeReturn;
            _grantExitInvulnerability = grantExitInvulnerability;
            _originalPosition = ship.Body.WorldPosition();
            Stop(ship.Body);
            ThreeBodySpatialVisuals.PlayUniverseTransit(_originalPosition, ship.Features.Color, false);
        }

        public bool IsAlive => _remaining > 0f;

        public static bool IsInTransit(IShip ship)
        {
            if (ship?.Effects == null) return false;
            foreach (var effect in ship.Effects.All)
                if (effect is SmallUniverseTransitEffect transit && transit.IsAlive)
                    return true;
            return false;
        }

        public void UpdatePhysics(IShip ship, float elapsedTime)
        {
            if (!IsAlive) return;

            var applied = Mathf.Min(_remaining, Mathf.Max(0f, elapsedTime));
            if (_drainResources)
            {
                ship.Stats.Armor.Get(ship.Stats.Armor.MaxValue * 0.015f * applied);
                ship.Stats.Energy.Get(ship.Stats.Energy.MaxValue * 0.015f * applied);
            }

            _remaining -= applied;
            Stop(ship.Body);
            if (_remaining > 0f || _returned) return;

            _returned = true;
            var destination = _safeReturn
                ? _scene.FindFreePlace(80f, ship.Type.Side)
                : _originalPosition;
            MoveWorld(ship.Body, destination);
            Stop(ship.Body);
            ThreeBodySpatialVisuals.PlayUniverseTransit(destination, ship.Features.Color, true);
            if (_grantExitInvulnerability)
                ship.AddEffect(new TemporaryInvulnerabilityEffect(5f));
        }

        public void UpdateView(IShip ship, float elapsedTime) { }
        public void Dispose() { }

        public bool TryApplyModification(ref FeaturesData data)
        {
            if (!IsAlive) return false;
            data.Opacity = 0f;
            data.TargetPriority = TargetPriority.None;
            data.Invulnerable = true;
            data.ImmuneToEffects = true;
            data.ColliderEnabled = false;
            return true;
        }

        public bool TryApplyModification(ref EngineData data)
        {
            if (!IsAlive) return false;
            data.Velocity = 0f;
            data.AngularVelocity = 0f;
            data.Propulsion = 0f;
            data.TurnRate = 0f;
            data.Throttle = 0f;
            data.HasCourse = false;
            return true;
        }

        bool ISystemsModification.IsAlive => IsAlive;
        public bool CanActivateSystem(ISystem system) => !IsAlive;
        public void OnSystemActivated(ISystem system) { }

        public IEngineModification EngineModification => this;
        public IFeaturesModification FeaturesModification => this;
        public ISystemsModification SystemsModification => this;
        public IStatsModification StatsModification => null;
        public IUnitAction UnitAction => null;

        private static void Stop(IBody body)
        {
            if (body is RigidBodyAdapter rigidBody)
            {
                rigidBody.Velocity = Vector2.zero;
                rigidBody.AngularVelocity = 0f;
            }
            else
            {
                body.ApplyAcceleration(-body.Velocity);
                body.ApplyAngularAcceleration(-body.AngularVelocity);
            }
        }

        private static void MoveWorld(IBody body, Vector2 worldPosition)
        {
            body.Move(body.Parent == null ? worldPosition : body.WorldPositionToLocal(worldPosition));
        }

        private readonly IShip _ship;
        private readonly IScene _scene;
        private readonly Vector2 _originalPosition;
        private readonly bool _drainResources;
        private readonly bool _safeReturn;
        private readonly bool _grantExitInvulnerability;
        private float _remaining;
        private bool _returned;
    }

    public sealed class TemporaryInvulnerabilityEffect : IShipEffect, IFeaturesModification
    {
        public TemporaryInvulnerabilityEffect(float duration) => _remaining = Mathf.Max(0f, duration);
        public bool IsAlive => _remaining > 0f;
        public void UpdatePhysics(IShip ship, float elapsedTime) => _remaining -= Mathf.Max(0f, elapsedTime);
        public void UpdateView(IShip ship, float elapsedTime) { }
        public void Dispose() { }
        public bool TryApplyModification(ref FeaturesData data)
        {
            if (!IsAlive) return false;
            data.Invulnerable = true;
            data.Color = Color.Lerp(data.Color, Color.white, 0.45f);
            return true;
        }
        public IEngineModification EngineModification => null;
        public IFeaturesModification FeaturesModification => this;
        public ISystemsModification SystemsModification => null;
        public IStatsModification StatsModification => null;
        public IUnitAction UnitAction => null;
        private float _remaining;
    }
}
