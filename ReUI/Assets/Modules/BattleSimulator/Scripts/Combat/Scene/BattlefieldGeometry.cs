using UnityEngine;

namespace Combat.Scene
{
    /// <summary>
    /// Distance helpers for the toroidal combat field.  Scene wrapping is
    /// performed around the active player, so a target just beyond one edge is
    /// physically adjacent to the corresponding point on the opposite edge.
    /// </summary>
    public static class BattlefieldGeometry
    {
        public static void Configure(SceneSettings settings)
        {
            _width = Mathf.Max(0f, settings.AreaWidth);
            _height = Mathf.Max(0f, settings.AreaHeight);
        }

        public static Vector2 Delta(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            delta.x = WrapDelta(delta.x, _width);
            delta.y = WrapDelta(delta.y, _height);
            return delta;
        }

        public static float Distance(Vector2 first, Vector2 second) => Delta(first, second).magnitude;
        public static float SqrDistance(Vector2 first, Vector2 second) => Delta(first, second).sqrMagnitude;

        public static Vector2 NearestEquivalent(Vector2 reference, Vector2 position)
        {
            return reference + Delta(reference, position);
        }

        private static float WrapDelta(float delta, float period)
        {
            if (period <= 0.001f)
                return delta;

            return Mathf.Repeat(delta + period * 0.5f, period) - period * 0.5f;
        }

        private static float _width;
        private static float _height;
    }
}
