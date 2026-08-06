using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gui.Quests
{
    /// <summary>
    /// Loads the six Beta6 storyline illustrations from base64 TextAssets.
    /// Text encoding keeps the assets patchable through the coding workspace;
    /// the decoded textures remain cached for the lifetime of the process.
    /// </summary>
    internal static class MothersTearsStoryImageCatalog
    {
        private const string ResourcePrefix = "Story/MothersTears/";
        private const string LegacyResourcePrefix = "Embedded/MothersTears/";

        public static bool TryLoad(string resourcePath, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(resourcePath) ||
                !resourcePath.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            if (Sprites.TryGetValue(resourcePath, out sprite) && sprite != null)
                return true;

            // Prefer the original high-resolution Sprite imported under
            // Resources. Beta6 originally embedded aggressively downscaled
            // JPEGs (as small as 256x144), which were then enlarged by the
            // full-screen story overlay and appeared visibly blurred.
            sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                Sprites[resourcePath] = sprite;
                return true;
            }

            var fileName = resourcePath.Substring(ResourcePrefix.Length);
            var encoded = Resources.Load<TextAsset>(LegacyResourcePrefix + fileName);
            if (encoded == null || string.IsNullOrWhiteSpace(encoded.text))
            {
                Debug.LogError("Mother's Tears story image is missing: " + resourcePath);
                return true;
            }

            try
            {
                var bytes = Convert.FromBase64String(encoded.text.Trim());
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
                {
                    name = resourcePath.Replace('/', '_'),
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontUnloadUnusedAsset,
                };
                if (!texture.LoadImage(bytes, true))
                {
                    UnityEngine.Object.Destroy(texture);
                    Debug.LogError("Mother's Tears story image could not be decoded: " + resourcePath);
                    return true;
                }

                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = texture.name;
                sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Sprites[resourcePath] = sprite;
                return true;
            }
            catch (FormatException error)
            {
                Debug.LogError("Mother's Tears story image has invalid embedded data: " +
                               resourcePath + "\n" + error.Message);
                return true;
            }
        }

        private static readonly Dictionary<string, Sprite> Sprites =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
