using System;
using Gui.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReUI
{
    internal static class ReUIHdrDisplaySettings
    {
        internal const string PreferenceKey = "ReUI.HdrPeakBrightness";

        private static bool _launchEnabled = ReadEnabled();

        internal static event Action<bool> Changed;

        internal static bool Enabled
        {
            get => ReadEnabled();
            set
            {
                if (Enabled == value) return;
                PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
                PlayerPrefs.Save();
                _launchEnabled = value;
                Changed?.Invoke(value);
                ReUIHdrRuntime.RefreshAllScenes();
            }
        }

        internal static bool LaunchEnabled => _launchEnabled;
        internal static bool RestartRequired => false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CaptureLaunchChoice()
        {
            _launchEnabled = ReadEnabled();
        }

        private static bool ReadEnabled()
        {
            // Native HDR is the default for this dedicated test build. Users
            // who explicitly selected standard brightness keep that choice.
            return PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
        }
    }

    /// <summary>
    /// Controls Unity's native HDR swap chain. No full-screen filter, bloom,
    /// colour grading pass or OnRenderImage hook is used here.
    /// </summary>
    internal static class ReUIHdrRuntime
    {
        private const string CombatScene = "CombatScene";
        // Keep ordinary combat art close to its SDR appearance while reserving
        // the extra HDR headroom for emissive weapon effects. A slightly higher
        // paper white also avoids the crushed, over-contrasted look seen on
        // OLED Android devices.
        private const float PaperWhiteNits = 240f;

        private static string _lastDiagnostic;
        private static string _lastWarning;
        private static bool _lastActive;
        private static float _nextPollTime;

        internal static event Action StatusChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeNativeOutput()
        {
            // Always boot menus in SDR. HDR is requested only while the combat
            // scene is loaded, otherwise Android tone mapping also changes UI.
            RequestHdrMode(false);
            Shader.SetGlobalFloat("_NativeHdrOutputActive", 0f);
            LogDiagnostics(true);
        }

        internal static void Apply(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            bool enableCameraHdr = ShouldUseHdr();
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

            RequestHdrMode(enableCameraHdr);
            UpdateShaderHdrState();
            LogDiagnostics(false);
        }

        internal static void RefreshAllScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Apply(SceneManager.GetSceneAt(i));
            bool shouldUseHdr = ShouldUseHdr();
            RequestHdrMode(shouldUseHdr);
            UpdateShaderHdrState();
        }

        internal static void Tick()
        {
            if (Time.unscaledTime < _nextPollTime) return;
            _nextPollTime = Time.unscaledTime + 0.1f;

            bool active = IsHdrOutputActive();
            bool shouldUseHdr = ShouldUseHdr();
            if (active != shouldUseHdr && IsHdrOutputAvailable())
                RequestHdrMode(shouldUseHdr);

            UpdateShaderHdrState();

            if (active != _lastActive)
            {
                _lastActive = active;
                RefreshAllScenes();
                StatusChanged?.Invoke();
            }

            LogDiagnostics(false);
        }

        internal static void RemoveAll()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
                if (cameras[i] != null) cameras[i].allowHDR = false;

            RequestHdrMode(false);
            Shader.SetGlobalFloat("_NativeHdrOutputActive", 0f);
        }

        internal static bool IsHdrOutputAvailable()
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

        internal static bool IsHdrOutputActive()
        {
            try
            {
                return HDROutputSettings.main != null && HDROutputSettings.main.active;
            }
            catch
            {
                return false;
            }
        }

        internal static string GetStatusText()
        {
            string text;
            try
            {
                HDROutputSettings output = HDROutputSettings.main;
                if (output == null || !output.available)
                {
                    text = "当前设备或图形接口未提供 HDR 输出";
                }
                else if (output.active)
                {
                    text = ReUIHdrDisplaySettings.LaunchEnabled
                        ? $"原生 HDR 已激活 · {output.displayColorGamut} · {output.graphicsFormat}"
                        : "原生 HDR 输出已激活 · 当前使用标准亮度";
                }
                else if (output.HDRModeChangeRequested)
                {
                    text = "已请求 HDR，等待 Android 系统切换";
                }
                else
                {
                    text = "HDR 输出未激活";
                }
            }
            catch (Exception exception)
            {
                text = "HDR 状态不可用：" + exception.GetType().Name;
            }

            return text;
        }

        private static bool ShouldUseHdr()
        {
            if (!ReUIHdrDisplaySettings.Enabled) return false;
            Scene combat = SceneManager.GetSceneByName(CombatScene);
            if (!combat.IsValid() || !combat.isLoaded) return false;

            // The replacement-ship chooser lives inside CombatScene, so a
            // simple scene-loaded check accidentally treated its ship sprites
            // as HDR combat content.  Switch the native output back to SDR for
            // that full-screen menu and restore HDR after it closes.
            var selection = UnityEngine.Object.FindFirstObjectByType<ShipSelectionPanel>(
                FindObjectsInactive.Include);
            return selection == null || !selection.IsOpen;
        }

        private static void UpdateShaderHdrState()
        {
            bool enabled = ShouldUseHdr() && IsHdrOutputActive();
            Shader.SetGlobalFloat("_NativeHdrOutputActive", enabled ? 1f : 0f);
        }

        private static void RequestHdrMode(bool enabled)
        {
            try
            {
                HDROutputSettings output = HDROutputSettings.main;
                if (output == null || !output.available) return;

                // An HDR swap chain still needs Unity's SDR-to-HDR mapping for
                // ordinary scene colours. Disabling it made gamma-space sprite
                // art get interpreted directly in the HDR gamut, producing
                // excessive contrast and saturation. It is enabled only while
                // combat HDR is requested; menus continue to use a real SDR
                // swap chain and receive no HDR filtering.
                output.automaticHDRTonemapping = enabled;
                output.paperWhiteNits = PaperWhiteNits;
                if (output.active != enabled)
                    output.RequestHDRModeChange(enabled);
            }
            catch (Exception exception)
            {
                if (_lastWarning == exception.Message) return;
                _lastWarning = exception.Message;
                Debug.LogWarning($"[ReUI HDR] Native HDR request failed: {exception.Message}");
            }
        }

        private static void LogDiagnostics(bool force)
        {
            string diagnostic;
            try
            {
                HDROutputSettings output = HDROutputSettings.main;
                diagnostic = output == null
                    ? $"api={SystemInfo.graphicsDeviceType}; support={SystemInfo.hdrDisplaySupportFlags}; output=null"
                    : $"api={SystemInfo.graphicsDeviceType}; support={SystemInfo.hdrDisplaySupportFlags}; " +
                      $"available={output.available}; active={output.active}; requested={output.HDRModeChangeRequested}; " +
                      $"gamut={output.displayColorGamut}; format={output.graphicsFormat}; " +
                      $"paperWhite={output.paperWhiteNits:0}; maxNits={output.maxToneMapLuminance}";
            }
            catch (Exception exception)
            {
                diagnostic = $"api={SystemInfo.graphicsDeviceType}; diagnostics={exception.GetType().Name}: {exception.Message}";
            }

            if (!force && diagnostic == _lastDiagnostic) return;
            _lastDiagnostic = diagnostic;
            Debug.Log("[ReUI HDR] " + diagnostic);
            StatusChanged?.Invoke();
        }

        private static bool IsUiOnlyCamera(Camera camera)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            return uiLayer >= 0 && camera.cullingMask == 1 << uiLayer;
        }
    }
}
