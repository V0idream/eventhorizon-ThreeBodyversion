using System;
using System.Collections.Generic;

namespace Combat.Component.Unit.Classification
{
    public static class CombatRelations
    {
        private static readonly Dictionary<long, bool> Relations = new();

        public static void SetRelation(int firstFaction, int secondFaction, bool allied)
        {
            Relations[Key(firstFaction, secondFaction)] = allied;
        }

        public static bool AreAllies(UnitType first, UnitType second)
        {
            if (first == null || second == null) return false;
            if (first.FactionId == second.FactionId) return true;
            if (Relations.TryGetValue(Key(first.FactionId, second.FactionId), out var allied)) return allied;

            var firstMod = first.FactionId >= 21;
            var secondMod = second.FactionId >= 21;
            if (first.FactionId == 0 || second.FactionId == 0)
                return firstMod || secondMod;
            return firstMod && secondMod;
        }

        public static bool AreEnemies(UnitType first, UnitType second) => !AreAllies(first, second);

        private static long Key(int first, int second)
        {
            var min = Math.Min(first, second);
            var max = Math.Max(first, second);
            return ((long)min << 32) | (uint)max;
        }
    }
}
