using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Services.Resources
{
    /// <summary>
    /// Stores artwork overrides made by the player in the ship editor.  The
    /// original artwork is never modified: the override is a separate PNG
    /// under the save directory and deleting it restores the database sprite.
    /// </summary>
    public static class PlayerShipTextureOverrides
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<int, string> SourceSignatures = new Dictionary<int, string>();
        private static readonly Dictionary<int, byte[]> RemoteBytes = new Dictionary<int, byte[]>();
        private static readonly Dictionary<int, Sprite> RemoteCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Texture2D> RemoteTextures = new Dictionary<int, Texture2D>();
        private const string FolderName = "PlayerShipTextures";

        public static bool HasConsent
        {
            get => PlayerPrefs.GetInt("ThreeBody.TextureCustomizationDisclaimer", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("ThreeBody.TextureCustomizationDisclaimer", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static Sprite Get(int shipId, Sprite fallback)
        {
            if (fallback == null)
                return fallback;

            var key = GetOverrideKey(shipId, fallback);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var path = ResolveOverridePath(shipId, fallback, key);
            if (!File.Exists(path))
                return fallback;

            try
            {
                var bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, true))
                {
                    DestroyObject(texture);
                    return fallback;
                }

                var normalizedTexture = NormalizeTextureForSprite(texture, fallback);
                if (!ReferenceEquals(normalizedTexture, texture))
                {
                    DestroyObject(texture);
                    texture = normalizedTexture;
                }

                texture.name = "PlayerShipTexture_" + key;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                Textures[key] = texture;
                var sprite = CreateOverrideSprite(texture, fallback);
                sprite.name = "PlayerShipTexture_" + key;
                Cache[key] = sprite;
                return sprite;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Unable to load player ship texture: " + error.Message);
                return fallback;
            }
        }

        public static bool Apply(int shipId, Sprite baseSprite, Texture2D overlay,
            bool sticker, float scale, Vector2 normalizedOffset, float rotationDegrees, out string error)
        {
            error = null;
            if (baseSprite == null || overlay == null)
            {
                error = "缺少舰船贴图或导入图片";
                return false;
            }

            Texture2D source = null;
            Texture2D layer = null;
            try
            {
                source = CopySprite(baseSprite);
                layer = CopyTexture(overlay);
                if (source == null || layer == null)
                {
                    error = "图片不可读";
                    return false;
                }

                var result = Compose(source, layer, sticker, scale, normalizedOffset, rotationDegrees);
                var key = GetOverrideKey(shipId, baseSprite);
                SaveOverride(key, result);
                ReplaceCache(key, result, baseSprite);
                DestroyObject(result);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                if (source != null) DestroyObject(source);
                if (layer != null) DestroyObject(layer);
            }
        }

        public static void Restore(int shipId)
        {
            Restore(shipId, null);
        }

        public static void Restore(int shipId, Sprite fallback)
        {
            IEnumerable<string> keys = fallback != null
                ? new[] { GetOverrideKey(shipId, fallback), GetLegacySourceKey(shipId, fallback) }
                : new List<string>(Cache.Keys).FindAll(key => key.StartsWith(shipId + "_", StringComparison.Ordinal));
            foreach (var key in new HashSet<string>(keys))
            {
                if (Cache.TryGetValue(key, out var sprite) && sprite != null)
                    DestroyObject(sprite);
                Cache.Remove(key);
                if (Textures.TryGetValue(key, out var texture) && texture != null)
                    DestroyObject(texture);
                Textures.Remove(key);

                var path = GetOverridePath(key);
                if (File.Exists(path)) File.Delete(path);
            }

            var legacy = GetLegacyOverridePath(shipId);
            if (File.Exists(legacy)) File.Delete(legacy);
        }

        public static bool HasOverride(int shipId, Sprite fallback)
        {
            if (fallback == null) return false;
            return File.Exists(ResolveOverridePath(shipId, fallback, GetOverrideKey(shipId, fallback)));
        }

        public static bool HasOverride(int shipId)
        {
            var directory = Path.Combine(Application.persistentDataPath, FolderName);
            if (File.Exists(GetLegacyOverridePath(shipId))) return true;
            return Directory.Exists(directory) && Directory.GetFiles(directory, shipId + "_*.png").Length > 0;
        }

        public static bool TryGetOverrideBytes(int shipId, out byte[] bytes)
        {
            bytes = null;
            var path = FindAnyOverridePath(shipId);
            if (!File.Exists(path)) return false;
            try { bytes = File.ReadAllBytes(path); return bytes.Length > 0; }
            catch (Exception error) { Debug.LogWarning("Unable to read player ship texture: " + error.Message); return false; }
        }

        public static bool TryGetOverrideBytes(int shipId, Sprite fallback, out byte[] bytes)
        {
            bytes = null;
            if (fallback == null) return false;
            var path = ResolveOverridePath(shipId, fallback, GetOverrideKey(shipId, fallback));
            if (!File.Exists(path)) return false;
            try { bytes = File.ReadAllBytes(path); return bytes.Length > 0; }
            catch (Exception error) { Debug.LogWarning("Unable to read player ship texture: " + error.Message); return false; }
        }

        public static void SetRemoteOverride(int shipId, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return;
            RemoteBytes[shipId] = bytes;
            if (RemoteCache.TryGetValue(shipId, out var sprite) && sprite != null) DestroyObject(sprite);
            if (RemoteTextures.TryGetValue(shipId, out var texture) && texture != null) DestroyObject(texture);
            RemoteCache.Remove(shipId);
            RemoteTextures.Remove(shipId);
        }

        public static Sprite GetRemote(int shipId, Sprite fallback)
        {
            if (!RemoteBytes.TryGetValue(shipId, out var bytes) || fallback == null) return fallback;
            if (RemoteCache.TryGetValue(shipId, out var cached) && cached != null) return cached;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes, true)) { DestroyObject(texture); return fallback; }
            var normalizedTexture = NormalizeTextureForSprite(texture, fallback);
            if (!ReferenceEquals(normalizedTexture, texture))
            {
                DestroyObject(texture);
                texture = normalizedTexture;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var sprite = CreateOverrideSprite(texture, fallback);
            RemoteTextures[shipId] = texture;
            RemoteCache[shipId] = sprite;
            return sprite;
        }

        public static void ClearRemoteSession()
        {
            foreach (var sprite in RemoteCache.Values) if (sprite != null) DestroyObject(sprite);
            foreach (var texture in RemoteTextures.Values) if (texture != null) DestroyObject(texture);
            RemoteBytes.Clear();
            RemoteCache.Clear();
            RemoteTextures.Clear();
        }

        public static Texture2D CreatePreview(Sprite baseSprite, Texture2D overlay,
            bool sticker, float scale, Vector2 normalizedOffset, float rotationDegrees = 0f)
        {
            var source = CopySprite(baseSprite);
            var layer = CopyTexture(overlay);
            if (source == null || layer == null)
            {
                if (source != null) DestroyObject(source);
                if (layer != null) DestroyObject(layer);
                return null;
            }

            var result = Compose(source, layer, sticker, scale, normalizedOffset, rotationDegrees);
            DestroyObject(source);
            DestroyObject(layer);
            return result;
        }

        public static Texture2D CreateBasePreview(Sprite baseSprite)
        {
            return CopySprite(baseSprite);
        }

        private static Texture2D Compose(Texture2D source, Texture2D layer, bool sticker,
            float scale, Vector2 normalizedOffset, float rotationDegrees)
        {
            var width = source.width;
            var height = source.height;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var basePixels = source.GetPixels32();
            var layerPixels = layer.GetPixels32();
            // Texture2D's initial contents are platform-dependent. Explicitly
            // clear every pixel so transparent parts of the original hull can
            // never become white in a saved player override.
            var resultPixels = new Color32[width * height];
            var sx = Mathf.Max(0.01f, scale) * layer.width;
            var sy = Mathf.Max(0.01f, scale) * layer.height;
            var centerX = width * (0.5f + normalizedOffset.x);
            var centerY = height * (0.5f + normalizedOffset.y);
            var radians = rotationDegrees * Mathf.Deg2Rad;
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    var baseColor = basePixels[index];
                    if (baseColor.a == 0)
                        continue;

                    // Undo the preview rotation before sampling the imported
                    // image so the persisted PNG exactly matches the gesture.
                    var dx = x + 0.5f - centerX;
                    var dy = y + 0.5f - centerY;
                    var u = (cosine * dx + sine * dy) / sx + 0.5f;
                    var v = (-sine * dx + cosine * dy) / sy + 0.5f;
                    if (u < 0 || u >= 1 || v < 0 || v >= 1)
                    {
                        resultPixels[index] = baseColor;
                        continue;
                    }

                    var lx = Mathf.Clamp(Mathf.FloorToInt(u * layer.width), 0, layer.width - 1);
                    var ly = Mathf.Clamp(Mathf.FloorToInt(v * layer.height), 0, layer.height - 1);
                    var layerColor = layerPixels[ly * layer.width + lx];
                    if (layerColor.a == 0)
                    {
                        resultPixels[index] = baseColor;
                        continue;
                    }

                    if (sticker)
                    {
                        var alpha = layerColor.a / 255f;
                        var inv = 1f - alpha;
                        resultPixels[index] = new Color32(
                            (byte)(layerColor.r * alpha + baseColor.r * inv),
                            (byte)(layerColor.g * alpha + baseColor.g * inv),
                            (byte)(layerColor.b * alpha + baseColor.b * inv),
                            baseColor.a);
                    }
                    else
                    {
                        resultPixels[index] = new Color32(layerColor.r, layerColor.g, layerColor.b, baseColor.a);
                    }
                }
            }

            result.SetPixels32(resultPixels);
            result.Apply(false, false);
            result.wrapMode = TextureWrapMode.Clamp;
            result.filterMode = FilterMode.Bilinear;
            return result;
        }

        private static void SaveOverride(string key, Texture2D texture)
        {
            var directory = Path.Combine(Application.persistentDataPath, FolderName);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(GetOverridePath(key), texture.EncodeToPNG());
        }

        private static void ReplaceCache(string key, Texture2D texture, Sprite fallback)
        {
            if (Cache.TryGetValue(key, out var oldSprite) && oldSprite != null)
                DestroyObject(oldSprite);
            if (Textures.TryGetValue(key, out var oldTexture) && oldTexture != null)
                DestroyObject(oldTexture);

            var cachedTexture = UnityEngine.Object.Instantiate(texture);
            cachedTexture.name = "PlayerShipTexture_" + key;
            Textures[key] = cachedTexture;
            Cache[key] = CreateOverrideSprite(cachedTexture, fallback);
        }

        private static string GetOverrideKey(int shipId, Sprite fallback)
        {
            var rect = fallback.rect;
            var spriteName = string.IsNullOrWhiteSpace(fallback.name) ? "unnamed" : fallback.name;
            var textureName = fallback.texture == null || string.IsNullOrWhiteSpace(fallback.texture.name)
                ? "texture"
                : fallback.texture.name;
            var name = spriteName + "_" + textureName;
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            if (name.Length > 48) name = name.Substring(0, 48);
            return $"{shipId}_{name}_{Mathf.RoundToInt(rect.width)}x{Mathf.RoundToInt(rect.height)}_" +
                   $"{GetSourceSignature(fallback)}";
        }

        private static string GetLegacySourceKey(int shipId, Sprite fallback)
        {
            Rect rect;
            try { rect = fallback.textureRect; }
            catch { rect = fallback.rect; }
            var spriteName = string.IsNullOrWhiteSpace(fallback.name) ? "unnamed" : fallback.name;
            var textureName = fallback.texture == null || string.IsNullOrWhiteSpace(fallback.texture.name)
                ? "texture"
                : fallback.texture.name;
            var name = spriteName + "_" + textureName;
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return $"{shipId}_{name}_{Mathf.RoundToInt(rect.x)}_{Mathf.RoundToInt(rect.y)}_" +
                   $"{Mathf.RoundToInt(rect.width)}x{Mathf.RoundToInt(rect.height)}";
        }

        private static string ResolveOverridePath(int shipId, Sprite fallback, string key)
        {
            // Never fall back from a source-scoped key to the old numeric-only
            // file here. External mods routinely reuse base-game numeric IDs;
            // applying a legacy 123.png to a different mod hull is precisely
            // the incompatibility this namespace prevents.
            var current = GetOverridePath(key);
            if (File.Exists(current)) return current;

            // Beta7 already used source-scoped files but did not include a
            // pixel signature or preserve trimmed-sprite geometry. Accept that
            // key only when the source has stable names. Runtime-imported mod
            // images are commonly unnamed; their old key consisted only of ID
            // and dimensions and can belong to an entirely different mod.
            if (string.IsNullOrWhiteSpace(fallback.name) ||
                fallback.texture == null ||
                string.IsNullOrWhiteSpace(fallback.texture.name))
                return current;

            return GetOverridePath(GetLegacySourceKey(shipId, fallback));
        }

        private static string FindAnyOverridePath(int shipId)
        {
            var legacy = GetLegacyOverridePath(shipId);
            if (File.Exists(legacy)) return legacy;
            var directory = Path.Combine(Application.persistentDataPath, FolderName);
            if (!Directory.Exists(directory)) return legacy;
            var files = Directory.GetFiles(directory, shipId + "_*.png");
            return files.Length > 0 ? files[0] : legacy;
        }

        private static string GetOverridePath(string key)
        {
            return Path.Combine(Application.persistentDataPath, FolderName, key + ".png");
        }

        private static string GetLegacyOverridePath(int shipId)
        {
            return Path.Combine(Application.persistentDataPath, FolderName, shipId + ".png");
        }

        private static Sprite CreateOverrideSprite(Texture2D texture, Sprite fallback)
        {
            var logicalSize = fallback.rect.size;
            var normalizedPivot = new Vector2(
                logicalSize.x > 0.01f ? fallback.pivot.x / logicalSize.x : 0.5f,
                logicalSize.y > 0.01f ? fallback.pivot.y / logicalSize.y : 0.5f);
            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                normalizedPivot,
                Mathf.Max(1f, fallback.pixelsPerUnit),
                0,
                SpriteMeshType.FullRect);
        }

        private static Texture2D NormalizeTextureForSprite(Texture2D texture, Sprite fallback)
        {
            var logicalWidth = Mathf.Max(1, Mathf.RoundToInt(fallback.rect.width));
            var logicalHeight = Mathf.Max(1, Mathf.RoundToInt(fallback.rect.height));
            if (texture.width == logicalWidth && texture.height == logicalHeight)
                return texture;

            Rect textureRect;
            Vector2 textureOffset;
            try
            {
                textureRect = fallback.textureRect;
                textureOffset = fallback.textureRectOffset;
            }
            catch
            {
                textureRect = fallback.rect;
                textureOffset = Vector2.zero;
            }

            var visibleWidth = Mathf.Max(1, Mathf.RoundToInt(textureRect.width));
            var visibleHeight = Mathf.Max(1, Mathf.RoundToInt(textureRect.height));
            if (texture.width != visibleWidth || texture.height != visibleHeight)
                return texture;

            var readable = CopyTexture(texture);
            if (readable == null)
                return texture;

            var result = new Texture2D(logicalWidth, logicalHeight, TextureFormat.RGBA32, false);
            var resultPixels = new Color32[logicalWidth * logicalHeight];
            var sourcePixels = readable.GetPixels32();
            var offsetX = Mathf.RoundToInt(textureOffset.x);
            var offsetY = Mathf.RoundToInt(textureOffset.y);
            CopyPixels(sourcePixels, texture.width, texture.height,
                resultPixels, logicalWidth, logicalHeight, offsetX, offsetY);
            result.SetPixels32(resultPixels);
            result.Apply(false, false);
            result.wrapMode = TextureWrapMode.Clamp;
            result.filterMode = texture.filterMode;
            DestroyObject(readable);
            return result;
        }

        private static string GetSourceSignature(Sprite sprite)
        {
            var instanceId = sprite.GetInstanceID();
            if (SourceSignatures.TryGetValue(instanceId, out var cached))
                return cached;

            unchecked
            {
                ulong hash = 1469598103934665603UL;
                AddHash(ref hash, Mathf.RoundToInt(sprite.rect.width));
                AddHash(ref hash, Mathf.RoundToInt(sprite.rect.height));
                AddHash(ref hash, Mathf.RoundToInt(sprite.pivot.x * 1000f));
                AddHash(ref hash, Mathf.RoundToInt(sprite.pivot.y * 1000f));
                AddHash(ref hash, Mathf.RoundToInt(sprite.pixelsPerUnit * 1000f));

                Texture2D copy = null;
                try
                {
                    copy = CopySprite(sprite);
                    if (copy != null)
                    {
                        var pixels = copy.GetPixels32();
                        var step = Mathf.Max(1, pixels.Length / 1024);
                        for (var index = 0; index < pixels.Length; index += step)
                        {
                            var pixel = pixels[index];
                            AddHash(ref hash, pixel.r);
                            AddHash(ref hash, pixel.g);
                            AddHash(ref hash, pixel.b);
                            AddHash(ref hash, pixel.a);
                        }
                    }
                }
                catch (Exception error)
                {
                    Debug.LogWarning("Unable to fingerprint source ship texture: " + error.Message);
                }
                finally
                {
                    if (copy != null) DestroyObject(copy);
                }

                cached = hash.ToString("X16");
                SourceSignatures[instanceId] = cached;
                return cached;
            }
        }

        private static void AddHash(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (byte)value;
                hash *= 1099511628211UL;
                hash ^= (byte)(value >> 8);
                hash *= 1099511628211UL;
                hash ^= (byte)(value >> 16);
                hash *= 1099511628211UL;
                hash ^= (byte)(value >> 24);
                hash *= 1099511628211UL;
            }
        }

        private static void CopyPixels(
            Color32[] source,
            int sourceWidth,
            int sourceHeight,
            Color32[] target,
            int targetWidth,
            int targetHeight,
            int targetOffsetX,
            int targetOffsetY)
        {
            for (var y = 0; y < sourceHeight; ++y)
            {
                var targetY = y + targetOffsetY;
                if (targetY < 0 || targetY >= targetHeight) continue;
                for (var x = 0; x < sourceWidth; ++x)
                {
                    var targetX = x + targetOffsetX;
                    if (targetX < 0 || targetX >= targetWidth) continue;
                    target[targetY * targetWidth + targetX] = source[y * sourceWidth + x];
                }
            }
        }

        private static Texture2D CopyTexture(Texture2D texture)
        {
            if (texture == null) return null;
            try
            {
                var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                copy.SetPixels32(texture.GetPixels32());
                copy.Apply(false, false);
                return copy;
            }
            catch
            {
                return CopyViaRenderTexture(texture, new Rect(0, 0, texture.width, texture.height));
            }
        }

        private static Texture2D CopySprite(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return null;
            // Paint on the sprite's logical rectangle, not just textureRect.
            // Imported mods and packed atlases may trim transparent margins;
            // dropping textureRectOffset and then recreating a centered sprite
            // shifts the hull relative to the component grid.
            Rect rect;
            Vector2 offset;
            try
            {
                rect = sprite.textureRect;
                offset = sprite.textureRectOffset;
            }
            catch
            {
                rect = sprite.rect;
                offset = Vector2.zero;
            }

            Texture2D visible;
            try
            {
                visible = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height),
                    TextureFormat.RGBA32, false);
                visible.SetPixels(sprite.texture.GetPixels(
                    Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y),
                    Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height)));
                visible.Apply(false, false);
            }
            catch
            {
                visible = CopyViaRenderTexture(sprite.texture, rect);
            }

            if (visible == null) return null;

            var logicalWidth = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.width));
            var logicalHeight = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.height));
            var offsetX = Mathf.RoundToInt(offset.x);
            var offsetY = Mathf.RoundToInt(offset.y);
            if (visible.width == logicalWidth && visible.height == logicalHeight &&
                offsetX == 0 && offsetY == 0)
                return visible;

            var result = new Texture2D(logicalWidth, logicalHeight, TextureFormat.RGBA32, false);
            var resultPixels = new Color32[logicalWidth * logicalHeight];
            CopyPixels(visible.GetPixels32(), visible.width, visible.height,
                resultPixels, logicalWidth, logicalHeight, offsetX, offsetY);
            result.SetPixels32(resultPixels);
            result.Apply(false, false);
            result.wrapMode = TextureWrapMode.Clamp;
            result.filterMode = sprite.texture.filterMode;
            DestroyObject(visible);
            return result;
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }

        private static Texture2D CopyViaRenderTexture(Texture2D texture, Rect sourceRect)
        {
            var width = Mathf.Max(1, Mathf.RoundToInt(sourceRect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(sourceRect.height));
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                var scale = new Vector2(sourceRect.width / texture.width, sourceRect.height / texture.height);
                var offset = new Vector2(sourceRect.x / texture.width, sourceRect.y / texture.height);
                Graphics.Blit(texture, target, scale, offset);
                RenderTexture.active = target;
                var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                result.Apply(false, false);
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
