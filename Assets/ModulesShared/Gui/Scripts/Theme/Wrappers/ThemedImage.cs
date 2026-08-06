using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gui.Theme.Wrappers
{
    [AddComponentMenu("UI/ThemedImage")]
    public class ThemedImage : Image
    {
        [SerializeField] private ThemeColor _themeColor;
        [SerializeField] private ThemeColorMode _colorMode;

        [NonSerialized] private bool _colorInitialized;
        [NonSerialized] private bool _themeManaged;

        public override Color color 
        {
            get => base.color;
            set
            {
                base.color = value;
                _colorInitialized = true;
                _themeManaged = false;
            }
        }

        protected override void Start()
        {
            base.Start();

#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying) return;
#endif

            try
            {
                if (!_colorInitialized && _themeColor != ThemeColor.Default)
                    ApplyThemeColor();
            }
            catch (System.Exception e)
            {
                GameDiagnostics.Debug.LogException(e, gameObject);
            }
        }

        public void RefreshThemeColor()
        {
            if (_themeColor == ThemeColor.Default)
                return;

            ApplyThemeColor();
        }

        private void ApplyThemeColor()
        {
            base.color = UiTheme.Current.GetColor(_themeColor).ApplyColorMode(_colorMode);
            _colorInitialized = true;
            _themeManaged = true;
        }
    }
}
