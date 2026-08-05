using System.Collections;
using System.Collections.Generic;
using Gui.Common;
using Gui.Theme;
using Gui.Theme.Wrappers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReUI
{
    [DefaultExecutionOrder(-9000)]
    public sealed class ReUIBootstrap : MonoBehaviour
    {
        public const string EnabledPreference = "ReUI.Enabled";
        private const string ShipEditorSceneName = "ShipEditorScene";
        private const string CombatSceneName = "CombatScene";
        private const string SettingsSceneName = "SettingsScene";

        private static ReUIBootstrap _instance;

        private static readonly ThemeColor[] ImageThemeColors =
        {
            ThemeColor.Window,
            ThemeColor.ScrollBar,
            ThemeColor.Icon,
            ThemeColor.Selection,
            ThemeColor.Button,
            ThemeColor.ButtonFocus,
            ThemeColor.ButtonIcon,
            ThemeColor.BackgroundDark,
            ThemeColor.Credits,
            ThemeColor.Tokens,
        };

        private static readonly ThemeColor[] TextThemeColors =
        {
            ThemeColor.Text,
            ThemeColor.HeaderText,
            ThemeColor.PaleText,
            ThemeColor.BrightText,
            ThemeColor.ButtonText,
            ThemeColor.Credits,
            ThemeColor.Tokens,
        };

        private static readonly ThemeColorMode[] ThemeModes =
        {
            ThemeColorMode.Default,
            ThemeColorMode.SemiTransparent25,
            ThemeColorMode.SemiTransparent50,
            ThemeColorMode.SemiTransparent75,
            ThemeColorMode.Brightness25,
            ThemeColorMode.Brightness50,
            ThemeColorMode.Brightbess75,
        };

        public static bool IsEnabled => PlayerPrefs.GetInt(EnabledPreference, 1) != 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (!IsEnabled || _instance != null) return;

            GameObject host = new("ReUI Runtime", typeof(ReUIBootstrap))
            {
                hideFlags = HideFlags.DontSave,
            };
            DontDestroyOnLoad(host);
            _instance = host.GetComponent<ReUIBootstrap>();
        }

        public static void SetEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(EnabledPreference, enabled ? 1 : 0);
            PlayerPrefs.Save();

            if (enabled)
            {
                Install();
                _instance?.ApplyNow();
            }
            else if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }
        }

        /// <summary>
        /// Refreshes the small, explicitly supported ReUI surface set after a
        /// local theme preference changes. It deliberately does not restyle
        /// generic menu canvases.
        /// </summary>
        public static void RefreshTheme()
        {
            if (!IsEnabled) return;
            ThemeSnapshot previousTheme = ThemeSnapshot.Capture();
            SynchronizeRuntimeTheme();
            RefreshLoadedThemeControls(previousTheme);
            if (!Application.isPlaying)
            {
                // Editor validation constructs temporary UI controls. Do not
                // create a DontDestroyOnLoad runtime host from that context.
                _instance?.ApplyNow(true);
                return;
            }
            Install();
            _instance?.ApplyNow(true);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            ApplyNow();
            StartCoroutine(ApplyAfterSceneInitialization(SceneManager.GetActiveScene()));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ThemeSnapshot previousTheme = ThemeSnapshot.Capture();
            SynchronizeRuntimeTheme();
            RefreshSceneTheme(scene, previousTheme);

            if (IsSupportedScene(scene.name))
                ApplyScene(scene);
            StartCoroutine(ApplyAfterSceneInitialization(scene));
        }

        public void ApplyNow()
        {
            ApplyNow(false);
        }

        private void ApplyNow(bool resetStyleFlags)
        {
            ThemeSnapshot previousTheme = ThemeSnapshot.Capture();
            SynchronizeRuntimeTheme();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                RefreshSceneTheme(scene, previousTheme);
                ApplyScene(scene, resetStyleFlags);
            }
        }

        private static void SynchronizeRuntimeTheme()
        {
            if (ReUIPalette.HasCustomThemeColor)
                UiTheme.Current.SetRuntimeAccent(ReUIPalette.ThemeColor);
            else
                UiTheme.Current.ClearRuntimeAccent();

            ThreeBodyUiPalette.RefreshLocalTheme();
        }

        private static void RefreshLoadedThemeControls(ThemeSnapshot previousTheme)
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                RefreshSceneTheme(SceneManager.GetSceneAt(sceneIndex), previousTheme);
        }

        private static void RefreshSceneTheme(Scene scene, ThemeSnapshot previousTheme)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];

                ThemedImage[] themedImages = root.GetComponentsInChildren<ThemedImage>(true);
                for (int imageIndex = 0; imageIndex < themedImages.Length; imageIndex++)
                    themedImages[imageIndex].RefreshThemeColor();

                ThemedText[] themedTexts = root.GetComponentsInChildren<ThemedText>(true);
                for (int textIndex = 0; textIndex < themedTexts.Length; textIndex++)
                    themedTexts[textIndex].RefreshThemeColor();

                Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
                for (int graphicIndex = 0; graphicIndex < graphics.Length; graphicIndex++)
                {
                    Graphic graphic = graphics[graphicIndex];
                    if (graphic is ThemedImage || graphic is ThemedText) continue;

                    ThemeColor[] candidates = graphic is Text ? TextThemeColors : ImageThemeColors;
                    if (TryMapThemeColor(graphic.color, candidates, previousTheme, out Color mapped))
                        graphic.color = mapped;
                }

                Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);
                for (int selectableIndex = 0; selectableIndex < selectables.Length; selectableIndex++)
                {
                    Selectable selectable = selectables[selectableIndex];
                    ColorBlock colors = selectable.colors;
                    bool changed = false;
                    if (TryMapThemeColor(colors.normalColor, ImageThemeColors, previousTheme, out Color normal))
                    {
                        colors.normalColor = normal;
                        changed = true;
                    }
                    if (TryMapThemeColor(colors.highlightedColor, ImageThemeColors, previousTheme, out Color highlighted))
                    {
                        colors.highlightedColor = highlighted;
                        changed = true;
                    }
                    if (TryMapThemeColor(colors.pressedColor, ImageThemeColors, previousTheme, out Color pressed))
                    {
                        colors.pressedColor = pressed;
                        changed = true;
                    }
                    if (TryMapThemeColor(colors.selectedColor, ImageThemeColors, previousTheme, out Color selected))
                    {
                        colors.selectedColor = selected;
                        changed = true;
                    }
                    if (TryMapThemeColor(colors.disabledColor, ImageThemeColors, previousTheme, out Color disabled))
                    {
                        colors.disabledColor = disabled;
                        changed = true;
                    }
                    if (changed) selectable.colors = colors;
                }

                Shadow[] shadows = root.GetComponentsInChildren<Shadow>(true);
                for (int shadowIndex = 0; shadowIndex < shadows.Length; shadowIndex++)
                {
                    Shadow shadow = shadows[shadowIndex];
                    if (TryMapThemeColor(shadow.effectColor, ImageThemeColors, previousTheme, out Color mapped))
                        shadow.effectColor = mapped;
                }
            }
        }

        private static bool TryMapThemeColor(Color current, ThemeColor[] candidates, ThemeSnapshot previousTheme,
            out Color mapped)
        {
            UiTheme theme = UiTheme.Current;
            for (int colorIndex = 0; colorIndex < candidates.Length; colorIndex++)
            {
                ThemeColor themeColor = candidates[colorIndex];
                Color authored = theme.GetAuthoredColor(themeColor);
                Color previous = previousTheme != null ? previousTheme.Get(themeColor) : authored;
                Color target = theme.GetColor(themeColor);

                for (int modeIndex = 0; modeIndex < ThemeModes.Length; modeIndex++)
                {
                    ThemeColorMode mode = ThemeModes[modeIndex];
                    if (!Approximately(current, authored.ApplyColorMode(mode)) &&
                        !Approximately(current, previous.ApplyColorMode(mode)))
                        continue;

                    mapped = target.ApplyColorMode(mode);
                    return true;
                }
            }

            mapped = current;
            return false;
        }

        private static bool Approximately(Color left, Color right)
        {
            const float tolerance = 0.035f;
            return Mathf.Abs(left.r - right.r) <= tolerance &&
                   Mathf.Abs(left.g - right.g) <= tolerance &&
                   Mathf.Abs(left.b - right.b) <= tolerance &&
                   Mathf.Abs(left.a - right.a) <= tolerance;
        }

        private static void ApplyScene(Scene scene, bool resetStyleFlags = false)
        {
            if (!scene.IsValid() || !scene.isLoaded || !IsSupportedScene(scene.name))
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Canvas[] canvases = roots[rootIndex].GetComponentsInChildren<Canvas>(true);
                for (int canvasIndex = 0; canvasIndex < canvases.Length; canvasIndex++)
                {
                    Canvas canvas = canvases[canvasIndex];
                    if (canvas == null || !canvas.gameObject.scene.IsValid()) continue;
                    if ((canvas.hideFlags & HideFlags.HideAndDontSave) != 0) continue;

                    if (scene.name == ShipEditorSceneName)
                        ReUIShipEditorStyler.Apply(canvas);
                    else if (scene.name == CombatSceneName)
                        ReUIHudStyler.Apply(canvas);
                    else if (scene.name == SettingsSceneName)
                    {
                        ReUIThemePalettePanel.EnsureForSettings(canvas);
                        ReUIShieldStyleSelector.EnsureForSettings(canvas);
                    }
                }
            }
        }

        private IEnumerator ApplyAfterSceneInitialization(Scene scene)
        {
            yield return null;
            RefreshSceneTheme(scene, ThemeSnapshot.Capture());
            ApplyScene(scene);
            yield return new WaitForEndOfFrame();
            RefreshSceneTheme(scene, ThemeSnapshot.Capture());
            ApplyScene(scene);
            yield return new WaitForSecondsRealtime(0.20f);
            RefreshSceneTheme(scene, ThemeSnapshot.Capture());
            ApplyScene(scene);
        }

        private sealed class ThemeSnapshot
        {
            private readonly Dictionary<ThemeColor, Color> _colors = new();

            internal static ThemeSnapshot Capture()
            {
                ThemeSnapshot snapshot = new();
                snapshot.Add(ImageThemeColors);
                snapshot.Add(TextThemeColors);
                return snapshot;
            }

            internal Color Get(ThemeColor themeColor)
            {
                return _colors.TryGetValue(themeColor, out Color color)
                    ? color
                    : UiTheme.Current.GetColor(themeColor);
            }

            private void Add(ThemeColor[] colors)
            {
                for (int index = 0; index < colors.Length; index++)
                {
                    ThemeColor color = colors[index];
                    if (!_colors.ContainsKey(color))
                        _colors.Add(color, UiTheme.Current.GetColor(color));
                }
            }
        }

        private static bool IsSupportedScene(string sceneName)
        {
            // Beta5 intentionally limited ReUI to the ship editor's component
            // list and the combat HUD. Settings is included solely to host the
            // local theme palette; it does not restyle authored controls.
            return sceneName == ShipEditorSceneName ||
                   sceneName == CombatSceneName ||
                   sceneName == SettingsSceneName;
        }
    }
}
