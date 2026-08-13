using UnityEngine;

namespace Combat.Component.View
{
    /// <summary>
    /// Emits selected luminous combat content above SDR white when Unity's
    /// native HDR output is active. This is scene content, not a screen filter.
    /// </summary>
    public static class NativeHdrContent
    {
        private const string PreferenceKey = "ReUI.HdrPeakBrightness";

        public static bool IsActive
        {
            get
            {
                if (PlayerPrefs.GetInt(PreferenceKey, 1) == 0) return false;
                var combat = UnityEngine.SceneManagement.SceneManager.GetSceneByName("CombatScene");
                if (!combat.IsValid() || !combat.isLoaded) return false;
                try
                {
                    var output = HDROutputSettings.main;
                    return output != null && output.available && output.active;
                }
                catch
                {
                    return false;
                }

            }
        }

        public static Color Scale(Color color, float intensity)
        {
            if (!IsActive || intensity <= 1f) return color;
            return new Color(color.r * intensity, color.g * intensity, color.b * intensity, color.a);
        }

        /// <summary>
        /// Converts a desired highlight luminance to an intensity relative to
        /// the display's SDR paper white, while respecting the reported HDR
        /// tone-map ceiling. This keeps bright combat effects meaningful on
        /// displays with different peak luminance instead of using one fixed
        /// multiplier everywhere.
        /// </summary>
        public static Color ScaleToNits(Color color, float targetNits)
        {
            if (!IsActive || targetNits <= 0f) return color;

            try
            {
                var output = HDROutputSettings.main;
                var paperWhite = output != null && output.paperWhiteNits > 1f
                    ? output.paperWhiteNits
                    : 240f;
                var maximum = output != null && output.maxToneMapLuminance > paperWhite
                    ? output.maxToneMapLuminance
                    : targetNits;
                var intensity = Mathf.Clamp(targetNits / paperWhite, 1f, maximum / paperWhite);
                return Scale(color, intensity);
            }
            catch
            {
                return Scale(color, targetNits / 240f);
            }
        }
        public static float IntensityForNits(float targetNits)
        {
            if (targetNits <= 0f) return 1f;
            var paperWhite = 240f;
            try
            {
                var output = HDROutputSettings.main;
                if (output != null && output.paperWhiteNits > 1f)
                    paperWhite = output.paperWhiteNits;
            }
            catch
            {
                // Use the configured paper white fallback.
            }
            return Mathf.Clamp(targetNits / paperWhite, 1f, 5f);
        }
    }
}
