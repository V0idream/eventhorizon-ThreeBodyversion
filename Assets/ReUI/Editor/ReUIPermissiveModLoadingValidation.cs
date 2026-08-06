using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GameDatabase;
using GameDatabase.Storage;
using UnityEditor;
using UnityEngine;
using RuntimeDatabase = GameDatabase.Database;

namespace ReUI.Editor
{
    internal static class ReUIPermissiveModLoadingValidation
    {
        [MenuItem("Tools/ReUI/Validate Permissive Mod Loading")]
        public static void Validate()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "ReUI-PermissiveMod-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            try
            {
                // The first file deliberately cannot be deserialized. A
                // permissive folder import must skip it and keep scanning.
                File.WriteAllText(Path.Combine(directory, "00-incompatible.json"), "{ incompatible json");
                File.WriteAllText(
                    Path.Combine(directory, "01-settings.json"),
                    "{\"ItemType\":102,\"DatabaseVersion\":99,\"DatabaseVersionMinor\":42," +
                    "\"ModName\":\"Forced Compatibility Probe\",\"ModId\":\"ForcedCompatibilityProbe\"," +
                    "\"ModVersion\":1}");

                var storage = new FolderDatabaseStorage(directory);
                if (storage.Version.Major != 99 || storage.Version.Minor != 42)
                    throw new InvalidOperationException("The probe mod version was not read correctly.");

                var loadContent = typeof(RuntimeDatabase).GetMethod(
                    "LoadContent",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (loadContent == null)
                    throw new MissingMethodException(typeof(RuntimeDatabase).FullName, "LoadContent");

                var database = new RuntimeDatabase();
                var content = loadContent.Invoke(database, new object[] { storage }) as DatabaseContent;
                if (content?.DatabaseSettings == null ||
                    content.DatabaseSettings.ModId != "ForcedCompatibilityProbe")
                    throw new InvalidOperationException(
                        "An incompatible database version was not force-loaded with the current schema.");

                Debug.Log(
                    "[Permissive Mod Validation] declaredVersion=99.42, " +
                    "invalidEntry=skipped, currentSchema=force-loaded");

                var unreadableFile = Path.Combine(directory, "not-a-compiled-mod.bin");
                File.WriteAllBytes(unreadableFile, new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89 });

                var permissiveDatabase = new RuntimeDatabase();
                if (!permissiveDatabase.TryAddModFromFile(unreadableFile))
                    throw new InvalidOperationException("Unreadable external mod file was rejected during import.");

                var mod = permissiveDatabase.AvailableMods.FirstOrDefault(item => item.Path == unreadableFile);
                if (string.IsNullOrEmpty(mod.Id))
                    throw new InvalidOperationException("Unreadable external mod file was not registered.");

                if (!permissiveDatabase.TryLoad(mod.Id, out var error))
                    throw new InvalidOperationException(
                        "Unreadable external mod file was registered but could not be loaded: " + error);

                Debug.Log(
                    "[Permissive Mod Validation] unreadableFile=registered-and-loadable-as-empty-mod");
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }
    }
}
