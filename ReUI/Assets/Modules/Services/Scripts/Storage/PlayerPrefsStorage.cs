using Ionic.Zlib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using UnityEngine;
using GameModel.Serialization;

namespace Services.Storage
{
    public class PlayerPrefsStorage : IDataStorage
    {
        public bool TryLoad(ISerializableGameData gameData, string mod)
        {
            var saveKey = GetSaveKey(mod);
            if (TryLoadByKey(saveKey, out var data))
                return TryDeserialize(data, gameData, mod, saveKey);

            // Older builds loaded mod saves from savegame.<mod id>, but wrote
            // every save back to the shared "savegame" key.  On the next
            // launch the mod-specific key was therefore missing and a new game
            // was created.  Only recover the shared value when this is the mod
            // that was active when the application last closed; this avoids
            // cloning the vanilla save into a newly selected external mod.
            if (CanRecoverLegacyModSave(mod) &&
                TryLoadByKey(_key, out data) &&
                TryDeserialize(data, gameData, mod, _key))
            {
                PlayerPrefs.SetString(saveKey, Convert.ToBase64String(data, Base64FormattingOptions.None));
                PlayerPrefs.Save();
                _currentSaveKey = saveKey;
                UnityEngine.Debug.Log(
                    "PlayerPrefsStorage.TryLoad: migrated legacy mod save to " + saveKey);
                return true;
            }

            return false;
        }

        public void Save(ISerializableGameData gameData)
        {
            try
            {
                var saveKey = GetSaveKey(gameData.ModId);
                if (_currentSaveKey == saveKey &&
                    _currentGameId == gameData.GameId &&
                    _currentVersion == gameData.DataVersion)
                {
                    UnityEngine.Debug.Log("PlayerPrefsStorage.Save: Game data not changed: " + gameData.GameId + "/" + gameData.DataVersion);
                    return;
                }

                UnityEngine.Debug.Log("PlayerPrefsStorage.Save: saving data " + gameData.GameId + "/" + gameData.DataVersion);

                var data = new List<byte>();

                data.AddRange(Helpers.Serialize(_formatId));
                data.AddRange(Helpers.Serialize(gameData.GameId));
                data.AddRange(Helpers.Serialize(gameData.TimePlayed));
                data.AddRange(Helpers.Serialize(gameData.DataVersion));
                data.AddRange(Helpers.Serialize(AppConfig.version));
                data.AddRange(ZlibStream.CompressBuffer(gameData.Serialize().ToArray()));

                var serializedData = Convert.ToBase64String(data.ToArray(), Base64FormattingOptions.None);
                PlayerPrefs.SetString(saveKey, serializedData);
                PlayerPrefs.Save();

                _currentSaveKey = saveKey;
                _currentGameId = gameData.GameId;
                _currentVersion = gameData.DataVersion;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e.Message);
            }
        }

        public bool TryImportOriginalSave(ISerializableGameData gameData, string mod)
        {
            if (string.IsNullOrEmpty(mod))
                return false;

            return TryLoadByKey(_key, out var data) &&
                   TryDeserialize(data, gameData, mod, _key);
        }

        private static string GetSaveKey(string mod)
        {
            return string.IsNullOrEmpty(mod) ? _key : _key + "." + mod;
        }

        private static bool CanRecoverLegacyModSave(string mod)
        {
            if (string.IsNullOrEmpty(mod))
                return false;

            var previouslyActiveMod = PlayerPrefs.GetString(_activeModKey, string.Empty);
            return mod.Equals(previouslyActiveMod, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryLoadByKey(string key, out byte[] data)
        {
            try
            {
                var dataString = PlayerPrefs.GetString(key);

                if (string.IsNullOrEmpty(dataString))
                {
                    data = null;
                    return false;
                }

                data = System.Convert.FromBase64String(dataString);
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e.Message);
                data = null;
                return false;
            }
        }

        private bool TryDeserialize(
            byte[] serializedData,
            ISerializableGameData gameData,
            string mod,
            string saveKey)
        {
            try
            {
                _currentGameId = -1;
                _currentVersion = -1;
                _currentSaveKey = null;

                var size = (uint)(serializedData.Length - 1);

                int index = 0;
                var formatId = Helpers.DeserializeInt(serializedData, ref index);
                var gameId = Helpers.DeserializeLong(serializedData, ref index);
                var time = Helpers.DeserializeLong(serializedData, ref index);
                var version = Helpers.DeserializeLong(serializedData, ref index);
                var gameVersion = Helpers.DeserializeString(serializedData, ref index);

                if (!gameData.TryDeserialize(gameId, time, version, mod, ZlibStream.UncompressBuffer(serializedData.Skip(index).ToArray()), 0))
                {
                    Debug.LogException(new IOException("PlayerPrefsStorage.Load: Data deserialization failed"));
                    return false;
                }

                _currentGameId = gameId;
                _currentVersion = version;
                _currentSaveKey = saveKey;

                UnityEngine.Debug.Log("PlayerPrefsStorage.TryDeserializeData: done - " + gameData.GameId);

                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e.Message);
                return false;
            }
        }

        private long _currentGameId;
        private long _currentVersion;
        private string _currentSaveKey;
        
        private const string _key = "savegame";
        private const string _activeModKey = "mod";

        private const int _formatId = 3;
    }
}
