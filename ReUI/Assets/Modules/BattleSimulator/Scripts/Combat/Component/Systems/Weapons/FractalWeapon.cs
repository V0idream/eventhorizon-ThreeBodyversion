using System;
using System.Collections.Generic;
using Combat.Collision;
using Combat.Component.Collider;
using Combat.Component.Platform;
using Combat.Component.Ship;
using Combat.Component.Systems.Devices;
using Combat.Component.Unit.Classification;
using Combat.Factory;
using Combat.Scene;
using Combat.Unit;
using GameDatabase.DataModel;
using UnityEngine;

namespace Combat.Component.Systems.Weapons
{
    /// <summary>
    /// CPU-side logical fractal barrage.  The 12-stage/24,576-point pattern is
    /// preserved, but points are lightweight structs rather than full Unity
    /// bullets with a GameObject, Rigidbody2D, Collider2D and controller each.
    /// One ParticleSystem renders all visible points and a spatial hash limits
    /// collision work to nearby physical ship colliders.
    /// </summary>
    public sealed class FractalWeapon : WeaponBase
    {
        public FractalWeapon(IWeaponPlatform platform, WeaponStats weaponStats,
            IBulletFactory bulletFactory, int keyBinding, IScene scene, IShip owner)
            : base(platform, weaponStats, bulletFactory, keyBinding)
        {
            _scene = scene;
            _owner = owner;
            MaxCooldown = weaponStats.FireRate > 0f ? 1f / weaponStats.FireRate : 0f;
            _energyConsumption = bulletFactory.Stats.EnergyCost;
        }

        public override bool CanBeActivated =>
            base.CanBeActivated && !_barrageActive &&
            Platform.EnergyPoints.Value >= _energyConsumption;

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            var step = Mathf.Max(0f, elapsedTime);
            if (_barrageActive)
                UpdateBarrage(step);

            if (!Active || !CanBeActivated || !Platform.EnergyPoints.TryGet(_energyConsumption))
                return;

            StartBarrage();
            TimeFromLastUse = 0f;
            InvokeTriggers(Combat.Component.Triggers.ConditionType.OnActivate);
        }

        protected override void OnUpdateView(float elapsedTime)
        {
            if (_barrageActive || _visiblePointCount > 0)
                UpdateRenderer();
        }

        protected override void OnDispose()
        {
            DestroyRenderer();
            ClearGrid();
        }

        private void StartBarrage()
        {
            _pointCount = 0;
            _familyCount = 0;
            _generation = 0;
            _generationTimer = 0f;
            _elapsed = 0f;
            _nextPatternIndex = 1;
            _visiblePointCount = 0;
            _barrageActive = true;

            Platform.Aim(Info.BulletSpeed, Info.Range, Info.RelativeVelocityEffect);
            Platform.OnShot();

            _baseAngle = Platform.Body.WorldRotation();
            _driftDirection = RotationHelpers.Direction(_baseAngle);
            var position = Platform.Body.WorldPosition();
            var familyStart = _pointCount;
            for (var arm = 0; arm < Symmetry; ++arm)
            {
                _points[_pointCount++] = new PointState
                {
                    Position = position,
                    Angle = _baseAngle + arm * SymmetryAngle,
                    Distance = 0f,
                    Alive = true,
                    Moving = true,
                };
            }

            _families[_familyCount++] = new FamilyState(familyStart, 0);
            EnsureRenderer();
        }

        private void UpdateBarrage(float elapsedTime)
        {
            _elapsed += elapsedTime;
            _generationTimer += elapsedTime;

            RebuildCollisionGrid();
            UpdatePoints(elapsedTime);

            while (_generation < MaxGeneration && _generationTimer >= GenerationInterval)
            {
                _generationTimer -= GenerationInterval;
                SplitGeneration();
                ++_generation;
            }

            if (_generation >= MaxGeneration && !HasMovingPoints())
            {
                _barrageActive = false;
                _visiblePointCount = 0;
                if (_particleSystem != null)
                    _particleSystem.Clear(false);
            }
        }

        private void UpdatePoints(float elapsedTime)
        {
            for (var i = 0; i < _pointCount; ++i)
            {
                var point = _points[i];
                if (!point.Alive || !point.Moving)
                    continue;

                point.Angle += RotationSpeed * elapsedTime;
                var velocity = RotationHelpers.Direction(point.Angle) * ProjectileSpeed +
                               _driftDirection * DriftSpeed;
                var delta = velocity * elapsedTime;
                var deltaLength = delta.magnitude;

                if (deltaLength > 0.0001f && point.Distance + deltaLength > MaxDistance)
                {
                    var remaining = Mathf.Max(0f, MaxDistance - point.Distance);
                    delta *= remaining / deltaLength;
                    deltaLength = remaining;
                }

                var from = point.Position;
                var to = from + delta;
                if (deltaLength > 0f && ResolveCollisions(ref point, from, to))
                {
                    _points[i] = point;
                    continue;
                }

                point.Position = to;
                point.Distance += deltaLength;
                if (point.Distance >= MaxDistance - 0.0001f)
                    point.Moving = false;
                _points[i] = point;
            }
        }

        private void SplitGeneration()
        {
            var existingFamilyCount = _familyCount;
            var phase = RotationSpeed * _elapsed;
            for (var familyIndex = 0; familyIndex < existingFamilyCount; ++familyIndex)
            {
                var family = _families[familyIndex];
                var anyAlive = false;
                for (var arm = 0; arm < Symmetry; ++arm)
                {
                    if (_points[family.StartIndex + arm].Alive)
                    {
                        anyAlive = true;
                        break;
                    }
                }

                if (!anyAlive || _familyCount >= _families.Length || _pointCount + Symmetry > _points.Length)
                    continue;

                var patternIndex = _nextPatternIndex++;
                var canonicalOffset = Mathf.Repeat(patternIndex * GoldenAngle, 360f);
                var childStart = _pointCount;
                for (var arm = 0; arm < Symmetry; ++arm)
                {
                    var parent = _points[family.StartIndex + arm];
                    var child = default(PointState);
                    if (parent.Alive)
                    {
                        child.Position = parent.Position;
                        child.Angle = _baseAngle + arm * SymmetryAngle + canonicalOffset + phase;
                        child.Distance = 0f;
                        child.Alive = true;
                        child.Moving = true;
                    }
                    _points[_pointCount++] = child;
                }

                _families[_familyCount++] = new FamilyState(childStart, patternIndex);
            }
        }

        private bool ResolveCollisions(ref PointState point, Vector2 from, Vector2 to)
        {
            var minX = Mathf.FloorToInt(Mathf.Min(from.x, to.x) / GridCellSize);
            var maxX = Mathf.FloorToInt(Mathf.Max(from.x, to.x) / GridCellSize);
            var minY = Mathf.FloorToInt(Mathf.Min(from.y, to.y) / GridCellSize);
            var maxY = Mathf.FloorToInt(Mathf.Max(from.y, to.y) / GridCellSize);
            var queryStamp = NextQueryStamp();

            for (var y = minY; y <= maxY; ++y)
            {
                for (var x = minX; x <= maxX; ++x)
                {
                    if (!_collisionGrid.TryGetValue(GridKey(x, y), out var candidates))
                        continue;

                    for (var i = 0; i < candidates.Count; ++i)
                    {
                        var proxyIndex = candidates[i];
                        if (_proxyVisitStamp[proxyIndex] == queryStamp)
                            continue;
                        _proxyVisitStamp[proxyIndex] = queryStamp;

                        var proxy = _collisionProxies[proxyIndex];
                        var target = proxy.Target;
                        if (target == null || !target.IsActive() || proxy.Collider == null || !proxy.Collider.enabled)
                            continue;
                        if (PointInsideCollider(proxy.Collider, from, proxy.Bounds))
                            continue;
                        if (!SegmentHitsCollider(proxy.Collider, from, to, proxy.Bounds))
                            continue;

                        if (HasActiveMirrorSea(target))
                        {
                            point.Position = EntryPointOnBounds(from, to, proxy.Bounds);
                            point.Alive = false;
                            point.Moving = false;
                            return true;
                        }

                        target.Affect(new Impact { KineticDamage = DamagePerCollider }, _owner);
                    }
                }
            }

            return false;
        }

        private void RebuildCollisionGrid()
        {
            ClearGrid();
            _proxyCount = 0;
            if (_scene == null || _owner == null)
                return;

            lock (_scene.Ships.LockObject)
            {
                var ships = _scene.Ships.Items;
                for (var shipIndex = 0; shipIndex < ships.Count; ++shipIndex)
                {
                    var ship = ships[shipIndex];
                    if (ship == null || !ship.IsActive() || ship == _owner ||
                        !CombatRelations.AreEnemies(_owner.Type, ship.Type))
                        continue;

                    if (ship.Collider is not CommonCollider commonCollider)
                        continue;

                    var colliders = commonCollider.PhysicalColliders;
                    for (var colliderIndex = 0; colliderIndex < colliders.Count; ++colliderIndex)
                    {
                        var collider = colliders[colliderIndex];
                        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                            continue;

                        EnsureProxyCapacity(_proxyCount + 1);
                        var bounds = collider.bounds;
                        var proxyIndex = _proxyCount++;
                        _collisionProxies[proxyIndex] = new CollisionProxy(ship, collider, bounds);

                        var minX = Mathf.FloorToInt(bounds.min.x / GridCellSize);
                        var maxX = Mathf.FloorToInt(bounds.max.x / GridCellSize);
                        var minY = Mathf.FloorToInt(bounds.min.y / GridCellSize);
                        var maxY = Mathf.FloorToInt(bounds.max.y / GridCellSize);
                        for (var y = minY; y <= maxY; ++y)
                            for (var x = minX; x <= maxX; ++x)
                                AddProxyToCell(GridKey(x, y), proxyIndex);
                    }
                }
            }
        }

        private void AddProxyToCell(long key, int proxyIndex)
        {
            if (!_collisionGrid.TryGetValue(key, out var list))
            {
                list = new List<int>(4);
                _collisionGrid.Add(key, list);
            }

            if (list.Count == 0)
                _usedGridKeys.Add(key);
            list.Add(proxyIndex);
        }

        private void ClearGrid()
        {
            for (var i = 0; i < _usedGridKeys.Count; ++i)
            {
                if (_collisionGrid.TryGetValue(_usedGridKeys[i], out var list))
                    list.Clear();
            }
            _usedGridKeys.Clear();
        }

        private void EnsureProxyCapacity(int required)
        {
            if (required <= _collisionProxies.Length)
                return;

            var size = Mathf.NextPowerOfTwo(required);
            Array.Resize(ref _collisionProxies, size);
            Array.Resize(ref _proxyVisitStamp, size);
        }

        private int NextQueryStamp()
        {
            ++_queryStamp;
            if (_queryStamp != int.MaxValue)
                return _queryStamp;

            Array.Clear(_proxyVisitStamp, 0, _proxyVisitStamp.Length);
            _queryStamp = 1;
            return _queryStamp;
        }

        private static bool HasActiveMirrorSea(IShip ship)
        {
            var systems = ship.Systems?.All;
            if (systems == null)
                return false;
            for (var i = 0; i < systems.Count; ++i)
                if (systems[i] is MirrorSeaFieldDevice mirror && mirror.IsFieldEnabled)
                    return true;
            return false;
        }

        private bool HasMovingPoints()
        {
            for (var i = 0; i < _pointCount; ++i)
                if (_points[i].Alive && _points[i].Moving)
                    return true;
            return false;
        }

        private void EnsureRenderer()
        {
            if (_particleSystem != null)
                return;

            _rendererObject = new GameObject("Returner Fractal Barrage");
            _rendererObject.hideFlags = HideFlags.DontSave;
            _particleSystem = _rendererObject.AddComponent<ParticleSystem>();

            var main = _particleSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxPoints;
            main.startLifetime = 60f;
            main.startSpeed = 0f;
            main.startSize = PointSize;

            var emission = _particleSystem.emission;
            emission.enabled = false;
            var shape = _particleSystem.shape;
            shape.enabled = false;

            var renderer = _rendererObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 3;
            var shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader != null)
            {
                _particleMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.DontSave,
                    mainTexture = CreateGlowTexture(),
                };
                renderer.sharedMaterial = _particleMaterial;
            }

            _particleSystem.Play(false);
        }

        private void UpdateRenderer()
        {
            EnsureRenderer();
            var count = 0;
            for (var i = 0; i < _pointCount; ++i)
            {
                var point = _points[i];
                if (!point.Alive || !point.Moving)
                    continue;

                ref var particle = ref _particleBuffer[count++];
                particle.position = point.Position;
                particle.startColor = PointColor;
                particle.startSize = PointSize;
                particle.startLifetime = 60f;
                particle.remainingLifetime = 60f;
            }

            _visiblePointCount = count;
            _particleSystem.SetParticles(_particleBuffer, count);
        }

        private static Texture2D CreateGlowTexture()
        {
            if (_glowTexture != null)
                return _glowTexture;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "RuntimeFractalGlow",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; ++y)
            {
                for (var x = 0; x < size; ++x)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _glowTexture = texture;
            return texture;
        }

        private void DestroyRenderer()
        {
            if (_rendererObject != null)
                UnityEngine.Object.Destroy(_rendererObject);
            if (_particleMaterial != null)
                UnityEngine.Object.Destroy(_particleMaterial);
            _rendererObject = null;
            _particleSystem = null;
            _particleMaterial = null;
        }

        private static bool Contains2D(Bounds bounds, Vector2 point)
        {
            return point.x >= bounds.min.x && point.x <= bounds.max.x &&
                   point.y >= bounds.min.y && point.y <= bounds.max.y;
        }

        private bool PointInsideCollider(Collider2D collider, Vector2 point, Bounds bounds)
        {
            if (!Contains2D(bounds, point))
                return false;

            if (collider is CircleCollider2D circle)
            {
                var local = (Vector2)circle.transform.InverseTransformPoint(point) - circle.offset;
                return local.sqrMagnitude <= circle.radius * circle.radius;
            }

            if (collider is BoxCollider2D box)
            {
                var local = (Vector2)box.transform.InverseTransformPoint(point) - box.offset;
                var half = box.size * 0.5f;
                return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
            }

            if (collider is PolygonCollider2D polygon)
            {
                var local = (Vector2)polygon.transform.InverseTransformPoint(point) - polygon.offset;
                var paths = GetPolygonPaths(polygon);
                for (var pathIndex = 0; pathIndex < paths.Length; ++pathIndex)
                    if (PointInPolygon(local, paths[pathIndex]))
                        return true;
                return false;
            }

            return Contains2D(bounds, point);
        }

        private bool SegmentHitsCollider(Collider2D collider, Vector2 from, Vector2 to, Bounds bounds)
        {
            if (!SegmentIntersectsBounds(from, to, bounds))
                return false;

            if (collider is CircleCollider2D circle)
            {
                var localFrom = (Vector2)circle.transform.InverseTransformPoint(from) - circle.offset;
                var localTo = (Vector2)circle.transform.InverseTransformPoint(to) - circle.offset;
                return DistanceToSegmentSquared(Vector2.zero, localFrom, localTo) <=
                       circle.radius * circle.radius;
            }

            if (collider is BoxCollider2D box)
            {
                var localFrom = (Vector2)box.transform.InverseTransformPoint(from) - box.offset;
                var localTo = (Vector2)box.transform.InverseTransformPoint(to) - box.offset;
                var half = box.size * 0.5f;
                return SegmentIntersectsRect(localFrom, localTo, -half, half);
            }

            if (collider is PolygonCollider2D polygon)
            {
                var localFrom = (Vector2)polygon.transform.InverseTransformPoint(from) - polygon.offset;
                var localTo = (Vector2)polygon.transform.InverseTransformPoint(to) - polygon.offset;
                var paths = GetPolygonPaths(polygon);
                for (var pathIndex = 0; pathIndex < paths.Length; ++pathIndex)
                {
                    var path = paths[pathIndex];
                    if (PointInPolygon(localFrom, path) || PointInPolygon(localTo, path) ||
                        SegmentIntersectsPolygon(localFrom, localTo, path))
                        return true;
                }
                return false;
            }

            return true;
        }

        private Vector2[][] GetPolygonPaths(PolygonCollider2D polygon)
        {
            var id = polygon.GetInstanceID();
            if (_polygonPaths.TryGetValue(id, out var paths) && paths.Length == polygon.pathCount)
                return paths;

            paths = new Vector2[polygon.pathCount][];
            for (var i = 0; i < paths.Length; ++i)
                paths[i] = polygon.GetPath(i);
            _polygonPaths[id] = paths;
            return paths;
        }

        private static float DistanceToSegmentSquared(Vector2 point, Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var lengthSquared = delta.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
                return (point - from).sqrMagnitude;
            var t = Mathf.Clamp01(Vector2.Dot(point - from, delta) / lengthSquared);
            return (point - (from + delta * t)).sqrMagnitude;
        }

        private static bool SegmentIntersectsRect(Vector2 from, Vector2 to, Vector2 min, Vector2 max)
        {
            var tMin = 0f;
            var tMax = 1f;
            var delta = to - from;
            if (!ClipAxis(from.x, delta.x, min.x, max.x, ref tMin, ref tMax))
                return false;
            return ClipAxis(from.y, delta.y, min.y, max.y, ref tMin, ref tMax);
        }

        private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
                return false;

            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                var intersects = (a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) /
                    ((b.y - a.y) + 0.000001f) + a.x;
                if (intersects)
                    inside = !inside;
            }
            return inside;
        }

        private static bool SegmentIntersectsPolygon(Vector2 from, Vector2 to, Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 2)
                return false;
            for (var i = 0; i < polygon.Length; ++i)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Length];
                if (SegmentsIntersect(from, to, a, b))
                    return true;
            }
            return false;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var r = b - a;
            var s = d - c;
            var denominator = Cross(r, s);
            var offset = c - a;
            if (Mathf.Abs(denominator) < 0.000001f)
                return false;
            var t = Cross(offset, s) / denominator;
            var u = Cross(offset, r) / denominator;
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static bool SegmentIntersectsBounds(Vector2 from, Vector2 to, Bounds bounds)
        {
            var tMin = 0f;
            var tMax = 1f;
            var delta = to - from;
            if (!ClipAxis(from.x, delta.x, bounds.min.x, bounds.max.x, ref tMin, ref tMax))
                return false;
            return ClipAxis(from.y, delta.y, bounds.min.y, bounds.max.y, ref tMin, ref tMax);
        }

        private static Vector2 EntryPointOnBounds(Vector2 from, Vector2 to, Bounds bounds)
        {
            var tMin = 0f;
            var tMax = 1f;
            var delta = to - from;
            ClipAxis(from.x, delta.x, bounds.min.x, bounds.max.x, ref tMin, ref tMax);
            ClipAxis(from.y, delta.y, bounds.min.y, bounds.max.y, ref tMin, ref tMax);
            return from + delta * Mathf.Clamp01(tMin);
        }

        private static bool ClipAxis(float origin, float delta, float min, float max,
            ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(delta) < 0.000001f)
                return origin >= min && origin <= max;

            var inverse = 1f / delta;
            var t1 = (min - origin) * inverse;
            var t2 = (max - origin) * inverse;
            if (t1 > t2)
                (t1, t2) = (t2, t1);
            tMin = Mathf.Max(tMin, t1);
            tMax = Mathf.Min(tMax, t2);
            return tMin <= tMax;
        }

        private static long GridKey(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }

        private struct PointState
        {
            public Vector2 Position;
            public float Angle;
            public float Distance;
            public bool Alive;
            public bool Moving;
        }

        private readonly struct FamilyState
        {
            public FamilyState(int startIndex, int patternIndex)
            {
                StartIndex = startIndex;
                PatternIndex = patternIndex;
            }

            public int StartIndex { get; }
            public int PatternIndex { get; }
        }

        private readonly struct CollisionProxy
        {
            public CollisionProxy(IShip target, Collider2D collider, Bounds bounds)
            {
                Target = target;
                Collider = collider;
                Bounds = bounds;
            }

            public IShip Target { get; }
            public Collider2D Collider { get; }
            public Bounds Bounds { get; }
        }

        private const int Symmetry = 6;
        private const int MaxGeneration = 12;
        private const int MaxFamilies = 1 << MaxGeneration;
        private const int MaxPoints = Symmetry * MaxFamilies;
        private const float SymmetryAngle = 60f;
        private const float GoldenAngle = 137.507764f;
        private const float GenerationInterval = 0.5f;
        private const float RotationSpeed = 60f;
        private const float ProjectileSpeed = 150f;
        private const float DriftSpeed = 50f;
        private const float MaxDistance = 200f;
        private const float DamagePerCollider = 5000f;
        private const float GridCellSize = 12f;
        // The first batched renderer pass kept the old sub-unit dot scale and
        // became difficult to read on a phone once the camera zoomed out. Keep
        // the cheap single-particle-system renderer, but make each logical shot
        // a clearly readable blue glow instead of a pin-prick.
        private const float PointSize = 1.4f;
        private static readonly Color PointColor = new(0.08f, 0.62f, 1f, 1f);

        private readonly IScene _scene;
        private readonly IShip _owner;
        private readonly float _energyConsumption;
        private readonly PointState[] _points = new PointState[MaxPoints];
        private readonly FamilyState[] _families = new FamilyState[MaxFamilies];
        private readonly ParticleSystem.Particle[] _particleBuffer = new ParticleSystem.Particle[MaxPoints];
        private readonly Dictionary<long, List<int>> _collisionGrid = new();
        private readonly List<long> _usedGridKeys = new();
        private readonly Dictionary<int, Vector2[][]> _polygonPaths = new();

        private CollisionProxy[] _collisionProxies = new CollisionProxy[64];
        private int[] _proxyVisitStamp = new int[64];
        private int _proxyCount;
        private int _queryStamp;
        private int _pointCount;
        private int _familyCount;
        private int _generation;
        private int _nextPatternIndex;
        private int _visiblePointCount;
        private float _generationTimer;
        private float _elapsed;
        private float _baseAngle;
        private Vector2 _driftDirection;
        private bool _barrageActive;

        private GameObject _rendererObject;
        private ParticleSystem _particleSystem;
        private Material _particleMaterial;
        private static Texture2D _glowTexture;
    }
}
