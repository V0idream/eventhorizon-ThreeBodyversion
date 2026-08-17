using System;
using System.Collections.Generic;
using System.Linq;
using Services.Storage;
using UnityEditor;
using UnityEngine;

namespace ReUI.Editor
{
    internal static class ReUIModSavePersistenceValidation
    {
        [MenuItem("Tools/ReUI/Validate Mod Save Persistence")]
        public static void Validate()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var firstMod = "ReUI_SaveProbe_A_" + suffix;
            var secondMod = "ReUI_SaveProbe_B_" + suffix;
            var firstKey = "savegame." + firstMod;
            var secondKey = "savegame." + secondMod;

            var originalSave = Capture("savegame");
            var originalActiveMod = Capture("mod");

            try
            {
                PlayerPrefs.DeleteKey(firstKey);
                PlayerPrefs.DeleteKey(secondKey);
                PlayerPrefs.SetString("savegame", "vanilla-save-sentinel");

                var storage = new PlayerPrefsStorage();
                var first = new ProbeGameData(firstMod, 101, 7, new byte[] { 1, 2, 3, 4 });
                storage.Save(first);

                Assert(PlayerPrefs.HasKey(firstKey), "The mod save was not written to its own key.");
                Assert(
                    PlayerPrefs.GetString("savegame") == "vanilla-save-sentinel",
                    "Saving a mod overwrote the vanilla save.");

                // Use the same game id and data version to prove that the save
                // cache also distinguishes databases by mod id.
                var second = new ProbeGameData(secondMod, 101, 7, new byte[] { 9, 8, 7 });
                storage.Save(second);
                Assert(PlayerPrefs.HasKey(secondKey), "A second mod was suppressed by the save cache.");

                var loaded = new ProbeGameData();
                Assert(
                    new PlayerPrefsStorage().TryLoad(loaded, firstMod),
                    "The mod-specific save could not be loaded.");
                Assert(loaded.ModId == firstMod, "The loaded save has the wrong mod id.");
                Assert(loaded.Payload.SequenceEqual(first.Payload), "The loaded payload is different.");

                // Reproduce the layout created by affected builds: the active
                // mod's save exists only in the shared vanilla key.
                var legacyData = PlayerPrefs.GetString(firstKey);
                PlayerPrefs.DeleteKey(firstKey);
                PlayerPrefs.SetString("savegame", legacyData);
                PlayerPrefs.SetString("mod", firstMod);

                var migrated = new ProbeGameData();
                Assert(
                    new PlayerPrefsStorage().TryLoad(migrated, firstMod),
                    "The legacy shared-key mod save was not recovered.");
                Assert(PlayerPrefs.HasKey(firstKey), "The recovered save was not migrated permanently.");
                Assert(migrated.Payload.SequenceEqual(first.Payload), "The migrated payload is different.");

                Debug.Log(
                    "[Mod Save Persistence Validation] separateKeys=true, " +
                    "crossModCache=true, legacyMigration=true");
            }
            finally
            {
                PlayerPrefs.DeleteKey(firstKey);
                PlayerPrefs.DeleteKey(secondKey);
                Restore("savegame", originalSave);
                Restore("mod", originalActiveMod);
                PlayerPrefs.Save();
            }
        }

        private static SavedPreference Capture(string key)
        {
            return new SavedPreference(PlayerPrefs.HasKey(key), PlayerPrefs.GetString(key));
        }

        private static void Restore(string key, SavedPreference preference)
        {
            if (preference.Exists)
                PlayerPrefs.SetString(key, preference.Value);
            else
                PlayerPrefs.DeleteKey(key);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct SavedPreference
        {
            public SavedPreference(bool exists, string value)
            {
                Exists = exists;
                Value = value;
            }

            public bool Exists { get; }
            public string Value { get; }
        }

        private sealed class ProbeGameData : ISerializableGameData
        {
            public ProbeGameData()
            {
                Payload = Array.Empty<byte>();
            }

            public ProbeGameData(string modId, long gameId, long dataVersion, byte[] payload)
            {
                ModId = modId;
                GameId = gameId;
                DataVersion = dataVersion;
                TimePlayed = 1234;
                Payload = payload;
            }

            public long GameId { get; private set; }
            public long TimePlayed { get; private set; }
            public long DataVersion { get; private set; }
            public string ModId { get; private set; }
            public byte[] Payload { get; private set; }

            public IEnumerable<byte> Serialize()
            {
                return Payload;
            }

            public bool TryDeserialize(
                long gameId,
                long timePlayed,
                long dataVersion,
                string modId,
                byte[] data,
                int startIndex)
            {
                GameId = gameId;
                TimePlayed = timePlayed;
                DataVersion = dataVersion;
                ModId = modId;
                Payload = data.Skip(startIndex).ToArray();
                return true;
            }
        }
    }
}
