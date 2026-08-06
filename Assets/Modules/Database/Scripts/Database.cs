using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDatabase.DataModel;
using GameDatabase.Storage;
using GameDatabase.Model;
using UnityEngine;

namespace GameDatabase
{
    public partial class Database
    {
        private IDataStorage _storage;
        private DatabaseContent _content;
        private readonly IDataStorage _defaultStorage = new ResourceDatabaseStorage(DefaultPath);
		private readonly IJsonSerializer _jsonSerializer = new UnityJsonSerializer();
        private readonly List<ModInfo> _mods = new List<ModInfo>();
        private const string DefaultPath = "Database";

        public event Action<IDataStorage> DatabaseLoading;
        public event Action DatabaseLoaded;

        public IEnumerable<ModInfo> AvailableMods => _mods;

        public string Name => DatabaseSettings.ModName ?? Storage.Name;
        public string Id => Storage.Id;
        public bool IsEditable => Storage.IsEditable;

        public IEnumerable<Faction> FactionsWithEmpty => FactionList.Prepend(Faction.Empty);

        private IDataStorage Storage => _storage ?? _defaultStorage;

        public bool TryAddModFromFile(string path)
        {
            try
            {
                var mod = OpenFileStoragePermissive(path);
                _mods.Add(new ModInfo(mod.Name, mod.Id, path));
            }
            catch (Exception e)
            {
                GameDiagnostics.Trace.LogError("invalid mod file - " + path);
                Debug.LogException(e);
                return false;
            }

            return true;
        }

        public void LookForMods()
        {
            _mods.Clear();

#if UNITY_ANDROID && !UNITY_EDITOR
            var path = Application.persistentDataPath + "/Mods/";
#elif UNITY_STANDALONE || UNITY_EDITOR
            var path = Application.dataPath + "/../Mods/";
#else
            var path = string.Empty;
            return;
#endif
            try
            {
                Directory.CreateDirectory(path);
                var info = new DirectoryInfo(path);
                foreach (var fileInfo in info.GetFiles("*", SearchOption.TopDirectoryOnly))
                    TryAddModFromFile(fileInfo.FullName);

                foreach (var directoryInfo in info.GetDirectories("*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var mod = new FolderDatabaseStorage(directoryInfo.FullName);
                        _mods.Add(new ModInfo(mod.Name, mod.Id, directoryInfo.FullName));
                    }
                    catch (Exception e)
                    {
                        GameDiagnostics.Trace.LogError("invalid database - " + directoryInfo.FullName);
                        Debug.LogException(e);
                    }
                }
            }
            catch (Exception e)
            {
                GameDiagnostics.Trace.LogError("Error loading mods - " + e.Message);
            }
        }

        public void LoadDefault()
        {
            try
            {
                Load();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public bool TryLoad(string id, out string error)
        {
            try
            {
                Clear();
                error = string.Empty;

                if (string.IsNullOrEmpty(id))
                {
                    Load();
                    return true;
                }

                var index = _mods.FindIndex(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    error = "Mod " + id + " is not found";
                    return false;
                }

                var path = _mods[index].Path;
                if (File.Exists(path))
                    Load(OpenFileStoragePermissive(path));
                else if (Directory.Exists(path))
                    Load(new FolderDatabaseStorage(path));
                else
                    throw new FileNotFoundException(path);

                return true;
            }
            catch (Exception e)
            {
                GameDiagnostics.Trace.LogException(e);
                GameDiagnostics.Trace.LogError("Database.TryLoad() Error: " + e.Message);
                error = e.Message;
                return false;
            }
        }

        private void Load(IDataStorage storage = null)
        {
            Clear();
            _storage = null;

            DatabaseLoading?.Invoke(storage);

            _content = LoadContent(storage);

            if (_content.DatabaseSettings == null || !_content.DatabaseSettings.UnloadOriginalDatabase)
                LoadExtraContent(_content, _defaultStorage);

            Loader.Load(this, _content);
            
            _storage = storage;
            DatabaseLoaded?.Invoke();
        }

        private static IDataStorage OpenFileStoragePermissive(string path)
        {
            try
            {
                return new FileDatabaseStorage(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"Forcing unreadable mod file '{path}' to be registered as an empty external mod." +
                    Environment.NewLine + e);
                return new PermissiveFileDatabaseStorage(path);
            }
        }

        private DatabaseContent LoadExtraContent(DatabaseContent content, IDataStorage storage)
        {
            if (storage == null)
                return content;

            if (storage.Version.Major == VersionMajor && storage.Version.Minor == VersionMinor)
            {
                content.LoadParent(storage);
                return content;
            }

            if (CanMigrate(storage.Version))
            {
                try
                {
                    var upgrader = new DatabaseMigration.DatabaseUpgrader(_jsonSerializer, storage);
                    upgrader.Upgrade(content);
                    return content;
                }
                catch (Exception e)
                {
                    LogForcedCompatibility(storage, "migration failed", e);
                }
            }
            else
            {
                LogForcedCompatibility(storage, "unsupported declared database version");
            }

            // Compatibility is intentionally not enforced. Unknown or future
            // database versions are parsed with the current schema and every
            // field the current game understands is retained.
            content.LoadParent(storage);
            return content;
        }

        private DatabaseContent LoadContent(IDataStorage storage)
        {
            if (storage == null)
                return new DatabaseContent(_jsonSerializer, null);

            if (storage.Version.Major == VersionMajor && storage.Version.Minor == VersionMinor)
                return new DatabaseContent(_jsonSerializer, storage);

            if (CanMigrate(storage.Version))
            {
                try
                {
                    var content = new DatabaseContent(_jsonSerializer, null);
                    var upgrader = new DatabaseMigration.DatabaseUpgrader(_jsonSerializer, storage);
                    upgrader.Upgrade(content);
                    return content;
                }
                catch (Exception e)
                {
                    LogForcedCompatibility(storage, "migration failed", e);
                }
            }
            else
            {
                LogForcedCompatibility(storage, "unsupported declared database version");
            }

            // Force-load incompatible mods as current-version content. The
            // serializers ignore fields unknown to this build, while storage
            // implementations skip individual entries they cannot deserialize.
            return new DatabaseContent(_jsonSerializer, storage);
        }

        private static bool CanMigrate(Storage.Version version)
        {
            // Version 0 is the legacy representation of 1.0. The generated
            // migration chain supports 1.0 through 1.6 and upgrades them to 1.7.
            if (version.Major == 0)
                return true;

            return version.Major == 1 &&
                   version.Minor >= 0 &&
                   version.Minor < VersionMinor;
        }

        private static void LogForcedCompatibility(
            IDataStorage storage,
            string reason,
            Exception exception = null)
        {
            var message =
                $"Forcing mod '{storage.Name}' ({storage.Id}) database " +
                $"version {storage.Version.Major}.{storage.Version.Minor} to load as " +
                $"{VersionMajor}.{VersionMinor}: {reason}.";

            if (exception == null)
                Debug.LogWarning(message);
            else
                Debug.LogWarning(message + Environment.NewLine + exception);
        }

        #region TODO: remove this after database editor can edit builds

        public void SaveShipBuild(ItemId<ShipBuild> id)
        {
            var serializedBuild = _content.GetShipBuild(id.Value);
            if (serializedBuild == null)
            {
                GameDiagnostics.Trace.LogError("SaveShipBuild: not found " + id);
                return;
            }

            var build = GetShipBuild(id);
            serializedBuild.Components = build.Components.Select(item => item.Serialize()).ToArray();
            Storage.UpdateItem(serializedBuild.FileName, _jsonSerializer.ToJson(serializedBuild));
        }

        public void SaveSatelliteBuild(ItemId<SatelliteBuild> id)
        {
            var serializedBuild = _content.GetSatelliteBuild(id.Value);
            if (serializedBuild == null)
            {
                GameDiagnostics.Trace.LogError("SaveSatelliteBuild: not found " + id);
                return;
            }

            var build = GetSatelliteBuild(id);
            serializedBuild.Components = build.Components.Select(item => item.Serialize()).ToArray();
            Storage.UpdateItem(serializedBuild.FileName, _jsonSerializer.ToJson(serializedBuild));
        }

#endregion
    }
}
