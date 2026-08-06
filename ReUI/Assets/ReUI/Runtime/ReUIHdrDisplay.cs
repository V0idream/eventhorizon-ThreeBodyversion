using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReUI
{
    internal static class ReUIHdrDisplaySettings
    {
        private const string PreferenceKey = "ReUI.HdrPeakBrightness";

        internal static event Action<bool> Changed;

        internal static bool Enabled
        {
            get => PlayerPrefs.GetInt(PreferenceKey, 0) != 0;
            set
            {
                if (Enabled == value) return;
                PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke(value);
                ReUIHdrRuntime.RefreshAllScenes();
            }
        }
    }

    /// <summary>
    /// Controls Unity's native HDR display output. This class intentionally
    /// contains no OnRenderImage hook or any other full-screen image effect.
    /// </summary>
    internal static class ReUIHdrRuntime
    {
        private const string StarMapScene = "StarMapScene";
        private const string CombatScene = "CombatScene";
        private static string _lastWarning;

        internal static void Apply(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            bool targetScene = scene.name == StarMapScene || scene.name == CombatScene;
            bool enableCameraHdr = targetScene && ReUIHdrDisplaySettings.Enabled && IsHdrOutputAvailable();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Camera[] cameras = roots[rootIndex].GetComponentsInChildren<Camera>(true);
                for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
                {
                    Camera camera = cameras[cameraIndex];
                    if (camera == null || IsUiOnlyCamera(camera)) continue;
                    camera.allowHDR = enableCameraHdr;
                }
            }

            UpdateHdrOutputMode();
        }

        internal static void RefreshAllScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Apply(SceneManager.GetSceneAt(i));
            UpdateHdrOutputMode();
        }

        internal static void RemoveAll()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
                if (cameras[i] != null) cameras[i].allowHDR = false;
            RequestHdrMode(false);
        }

        private static void UpdateHdrOutputMode()
        {
            bool targetSceneLoaded = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                if (scene.name == StarMapScene || scene.name == CombatScene)
                {
                    targetSceneLoaded = true;
                    break;
                }
            }

            RequestHdrMode(ReUIHdrDisplaySettings.Enabled && targetSceneLoaded && IsHdrOutputAvailable());
        }

        private static bool IsHdrOutputAvailable()
        {
            try
            {
                return HDROutputSettings.main != null && HDROutputSettings.main.available;
            }
            catch
            {
                return false;
            }
        }

        private static void RequestHdrMode(bool enabled)
        {
            try
            {
                HDROutputSettings output = HDROutputSettings.main;
                if (output == null) return;

                if (enabled)
                {
                    // This is Unity's real HDR swap-chain/output conversion.
                    // There is no bloom, threshold, colour grading or custom
                    // full-screen pass in the ReUI HDR implementation.
                    output.automaticHDRTonemapping = true;
                    if (!output.active || !output.HDRModeChangeRequested)
                        output.RequestHDRModeChange(true);
                }
                else if (output.active || output.HDRModeChangeRequested)
                {
                    output.RequestHDRModeChange(false);
                }
            }
            catch (Exception exception)
            {
                if (_lastWarning == exception.Message) return;
                _lastWarning = exception.Message;
                Debug.LogWarning($"[ReUI HDR] Display HDR mode change was unavailable: {exception.Message}");
            }
        }

        private static bool IsUiOnlyCamera(Camera camera)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            return uiLayer >= 0 && camera.cullingMask == 1 << uiLayer;
        }
    }
}
