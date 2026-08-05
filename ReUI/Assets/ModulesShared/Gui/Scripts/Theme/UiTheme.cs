using UnityEngine;

namespace Gui.Theme
{
    public enum ThemeColor
    {
        Default = 0,

        Window = 1,
        ScrollBar = 2,
        Icon = 3,
        Selection = 6,

        Button = 50,
        ButtonFocus = 51,
        ButtonText = 52,
        ButtonIcon = 53,

        WarningButton = 60,
        WarningButtonFocus = 61,
        WarningButtonText = 62,
        WarningButtonIcon = 63,

        PremiumButton = 70,
        PremiumButtonFocus = 71,
        PremiumButtonText = 72,
        PremiumButtonIcon = 73,

        BackgroundDark = 90,

        Text = 100,
        HeaderText = 101,
        PaleText = 102,
        BrightText = 103,
        ErrorText = 104,

        Credits = 200,
        Stars = 201,
        Money = 202,
        Fuel = 203,
        Tokens = 204,
        Snowflakes = 205,
    }

    public enum TechColor
    {
        Available = 1,
        NotAvailable = 2,
        Obtained = 3,
        Hidden = 4,
    }

    public enum ThemeColorMode
    {
        Default = 0,
     
        SemiTransparent25 = 1,
        SemiTransparent50 = 2,
        SemiTransparent75 = 3,

        Brightness25 = 12,
        Brightness50 = 11,
        Brightbess75 = 10,
    }

    public enum ThemeFont
    {
        Default = 0,

        Font1 = 1,
        Font2 = 2,
        Font3 = 3,
    }

    public enum ThemeFontSize
    {
        Default = 0,

        ButtonText24 = 1,
        NormalText24 = 3,
        TitleText26 = 5,
        LargeText38 = 2,
        LargeText32 = 8,
        CompactText22 = 6,
        SmallText20 = 4,
        SmallText18 = 7,
    }

    [CreateAssetMenu(fileName = "UiTheme", menuName = "AppConfig/UiTheme")]
    public class UiTheme : ScriptableObject
    {
        private static UiTheme _current;
        private Font _defaultFont;
        [System.NonSerialized] private bool _runtimeAccentEnabled;
        [System.NonSerialized] private Color _runtimeAccent = Color.white;

        [SerializeField] private Color _windowColor = new Color32(80,192,255,255);
        [SerializeField] private Color _scrollBarColor = new Color32(80, 192, 255, 192);
        [SerializeField] private Color _iconColor = new Color32(128, 255, 255, 255);
        [SerializeField] private Color _selectionColor = new Color32(128, 255, 255, 255);

        [SerializeField] private Color _backgroundDark = Color.black;

        [SerializeField] private Color _buttonColor = new Color32(80, 192, 255, 255);
        [SerializeField] private Color _buttonFocusColor = new Color32(80, 192, 255, 64);
        [SerializeField] private Color _buttonTextColor = new Color32(128, 255, 255, 255);
        [SerializeField] private Color _buttonIconColor = new Color32(128, 255, 255, 224);

        [SerializeField] private Color _warningButtonColor = new Color32(255, 128, 80, 255);
        [SerializeField] private Color _warningButtonFocusColor = new Color32(255, 192, 80, 32);
        [SerializeField] private Color _warningButtonTextColor = new Color32(255, 255, 192, 255);
        [SerializeField] private Color _warningButtonIconColor = new Color32(255, 255, 192, 255);

        [SerializeField] private Color _premiumButtonColor = new Color32(255, 255, 192, 255);
        [SerializeField] private Color _premiumButtonFocusColor = new Color32(255, 255, 192, 64);
        [SerializeField] private Color _premiumButtonTextColor = new Color32(255, 255, 224, 255);
        [SerializeField] private Color _premiumButtonIconColor = new Color32(255, 255, 192, 255);

        [SerializeField] private Color _textColor = new Color32(128, 255, 255, 255);
        [SerializeField] private Color _errorTextColor = new Color32(255, 192, 0, 255);
        [SerializeField] private Color _headerTextColor = new Color32(255, 255, 192, 255);
        [SerializeField] private Color _paleTextColor = new Color32(255, 255, 255, 160);
        [SerializeField] private Color _brightTextColor = new Color32(255, 255, 255, 255);

        [SerializeField] private Color _itemLowQualityColor = new Color32(192, 192, 192, 255);
        [SerializeField] private Color _itemCommonQualityColor = new Color32(128, 255, 255, 255);
        [SerializeField] private Color _itemMediumQualityColor = new Color32(128, 255, 128, 255);
        [SerializeField] private Color _itemHighQualityColor = new Color32(240, 159, 255, 255);
        [SerializeField] private Color _itemPerfectQualityColor = new Color32(255, 223, 81, 255);

        [SerializeField] private Color _availableTechColor = new Color32(255, 255, 192, 255);
        [SerializeField] private Color _unavailableTechColor = new Color32(128, 128, 128, 255);
        [SerializeField] private Color _obtainedTechColor = new Color32(80, 192, 255, 255);
        [SerializeField] private Color _hiddenTechColor = new Color32(128, 128, 255, 255);

        [SerializeField] private Color _creditsColor = new Color32(0, 255, 0, 255);
        [SerializeField] private Color _starsColor = new Color32(255, 240, 160, 255);
        [SerializeField] private Color _moneyColor = new Color32(255, 240, 160, 255);
        [SerializeField] private Color _tokensColor = new Color32(128, 128, 255, 255);
        [SerializeField] private Color _fuelColor = new Color32(0, 255, 255, 255);
        [SerializeField] private Color _snowflakesColor = new Color32(192, 255, 255, 255);

        [SerializeField] private int _smallText_18 = 18;
        [SerializeField] private int _smallText_20 = 20;
        [SerializeField] private int _compactText_22 = 22;
        [SerializeField] private int _buttonText_24 = 24;
        [SerializeField] private int _normalText_24 = 24;
        [SerializeField] private int _titleText_26 = 26;
        [SerializeField] private int _largeText_32 = 32;
        [SerializeField] private int _largeText_38 = 38;

        [SerializeField] private FontInfo _font1;
        [SerializeField] private FontInfo _font2;
        [SerializeField] private FontInfo _font3;

        public static UiTheme Current
        {
            get
            {
                if (_current == null)
                    _current = CreateInstance<UiTheme>();
                return _current;
            }
            set
            {
                _current = value;
            }
        }

        public Color GetColor(ThemeColor themeColor)
        {
            return ApplyRuntimeAccent(themeColor, GetAuthoredColor(themeColor));
        }

        public Color GetAuthoredColor(ThemeColor themeColor)
        {
            switch (themeColor)
            {
                case ThemeColor.Window: return _windowColor;
                case ThemeColor.ScrollBar: return _scrollBarColor;
                case ThemeColor.Icon: return _iconColor;
                case ThemeColor.Selection: return _selectionColor;

                case ThemeColor.BackgroundDark: return _backgroundDark;

                case ThemeColor.Button: return _buttonColor;
                case ThemeColor.ButtonFocus: return _buttonFocusColor;
                case ThemeColor.ButtonText: return _buttonTextColor;
                case ThemeColor.ButtonIcon: return _buttonIconColor;

                case ThemeColor.WarningButton: return _warningButtonColor;
                case ThemeColor.WarningButtonFocus: return _warningButtonFocusColor;
                case ThemeColor.WarningButtonText: return _warningButtonTextColor;
                case ThemeColor.WarningButtonIcon: return _warningButtonIconColor;

                case ThemeColor.PremiumButton: return _premiumButtonColor;
                case ThemeColor.PremiumButtonFocus: return _premiumButtonFocusColor;
                case ThemeColor.PremiumButtonText: return _premiumButtonTextColor;
                case ThemeColor.PremiumButtonIcon: return _premiumButtonIconColor;

                case ThemeColor.Text: return _textColor;
                case ThemeColor.HeaderText: return _headerTextColor;
                case ThemeColor.PaleText: return _paleTextColor;
                case ThemeColor.BrightText: return _brightTextColor;
                case ThemeColor.ErrorText: return _errorTextColor;

                case ThemeColor.Credits: return _creditsColor;
                case ThemeColor.Stars: return _starsColor;
                case ThemeColor.Money: return _moneyColor;
                case ThemeColor.Fuel: return _fuelColor;
                case ThemeColor.Tokens: return _tokensColor;
                case ThemeColor.Snowflakes: return _snowflakesColor;
                default:
                    throw new System.InvalidOperationException($"Invalid color type {themeColor}");
            }
        }

        /// <summary>
        /// Applies a local presentation accent without mutating database JSON or
        /// save data. Semantic warning, premium and resource colours remain
        /// authored; only the ordinary interface palette follows the selection.
        /// </summary>
        public void SetRuntimeAccent(Color accent)
        {
            accent.a = 1f;
            _runtimeAccent = accent;
            _runtimeAccentEnabled = true;
        }

        public void ClearRuntimeAccent()
        {
            _runtimeAccentEnabled = false;
        }

        public int GetFontSize(ThemeFontSize themeFontSize)
        {
            switch (themeFontSize)
            {
                case ThemeFontSize.SmallText18: return _smallText_18;
                case ThemeFontSize.SmallText20: return _smallText_20;
                case ThemeFontSize.CompactText22: return _compactText_22;
                case ThemeFontSize.ButtonText24: return _buttonText_24;
                case ThemeFontSize.NormalText24: return _normalText_24;
                case ThemeFontSize.TitleText26: return _titleText_26;
                case ThemeFontSize.LargeText32: return _largeText_32;
                case ThemeFontSize.LargeText38: return _largeText_38;
            }

            throw new System.InvalidOperationException($"Invalid element type {themeFontSize}");
        }

        public FontInfo GetFont(ThemeFont themeFont)
        {
            FontInfo fontInfo = new();
            switch (themeFont)
            {
                case ThemeFont.Font1: fontInfo = _font1; break;
                case ThemeFont.Font2: fontInfo = _font2; break;
                case ThemeFont.Font3: fontInfo = _font3; break;
                default:
                    throw new System.InvalidOperationException($"Invalid font type {themeFont}");
            }

            if (fontInfo.Font == null)
            {
                GameDiagnostics.Debug.LogError($"Font {themeFont} is not defined. Replaced with the built-in font");

                if (_defaultFont == null) 
                    _defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

                fontInfo.Font = _defaultFont;
                fontInfo.SizeMultiplier = 1.0f;
            }

            return fontInfo;
        }

        public Color GetQualityColor(Economy.ItemType.ItemQuality quality)
        {
            switch (quality)
            {
                case Economy.ItemType.ItemQuality.Low: return _itemLowQualityColor;
                case Economy.ItemType.ItemQuality.Common:
                    return ApplyRuntimeAccent(ThemeColor.Icon, _itemCommonQualityColor);
                case Economy.ItemType.ItemQuality.Medium: return _itemMediumQualityColor;
                case Economy.ItemType.ItemQuality.High: return _itemHighQualityColor;
                case Economy.ItemType.ItemQuality.Perfect: return _itemPerfectQualityColor;
            }

            throw new System.InvalidOperationException($"Invalid quality value {quality}");
        }

        public Color GetCurrencyColor(Economy.Currency currency)
        {
            switch (currency)
            {
                case Economy.Currency.Credits: return ApplyRuntimeAccent(ThemeColor.Credits, _creditsColor);
                case Economy.Currency.Stars: return _starsColor;
                case Economy.Currency.Money: return _moneyColor;
                case Economy.Currency.Tokens: return ApplyRuntimeAccent(ThemeColor.Tokens, _tokensColor);
                case Economy.Currency.Snowflakes: return _snowflakesColor;
                case Economy.Currency.None: return Color.white;
            }

            throw new System.InvalidOperationException($"Invalid currency {currency}");
        }

        public Color GetTechColor(TechColor techColor)
        {
            switch (techColor)
            {
                case TechColor.Available: return ApplyRuntimeAccent(ThemeColor.HeaderText, _availableTechColor);
                case TechColor.NotAvailable: return _unavailableTechColor;
                case TechColor.Obtained: return ApplyRuntimeAccent(ThemeColor.Window, _obtainedTechColor);
                case TechColor.Hidden: return ApplyRuntimeAccent(ThemeColor.Selection, _hiddenTechColor);
            }

            throw new System.InvalidOperationException($"Invalid TechColor value {techColor}");
        }

        public void Import(GameDatabase.IDatabase database)
        {
            var settings = database.UiSettings;

            _windowColor = settings.WindowColor;
            _scrollBarColor = settings.ScrollBarColor;
            _iconColor = settings.IconColor;
            _selectionColor = settings.SelectionColor;

            _backgroundDark = settings.BackgroundDark;

            _buttonColor = settings.ButtonColor;
            _buttonFocusColor = settings.ButtonFocusColor;
            _buttonTextColor = settings.ButtonTextColor;
            _buttonIconColor = settings.ButtonIconColor;

            _warningButtonColor = settings.WarningButtonColor;
            _warningButtonFocusColor = settings.WarningButtonFocusColor;
            _warningButtonTextColor = settings.WarningButtonTextColor;
            _warningButtonIconColor = settings.WarningButtonIconColor;

            _premiumButtonColor = settings.PremiumButtonColor;
            _premiumButtonFocusColor = settings.PremiumButtonFocusColor;
            _premiumButtonTextColor = settings.PremiumButtonTextColor;
            _premiumButtonIconColor = settings.PremiumButtonIconColor;

            _availableTechColor = settings.AvailableTechColor;
            _unavailableTechColor = settings.UnavailableTechColor;
            _obtainedTechColor = settings.ObtainedTechColor;
            _hiddenTechColor = settings.HiddenTechColor;

            _creditsColor = settings.CreditsColor;
            _starsColor = settings.StarsColor;
            _moneyColor = settings.MoneyColor;
            _fuelColor = settings.FuelColor;
            _tokensColor = settings.TokensColor;

            _errorTextColor = settings.ErrorTextColor;

            _textColor = settings.TextColor;
            _headerTextColor = settings.HeaderTextColor;
            _paleTextColor = settings.PaleTextColor;
            _brightTextColor = settings.BrightTextColor;

            _itemLowQualityColor = settings.LowQualityItemColor;
            _itemCommonQualityColor = settings.CommonQualityItemColor;
            _itemMediumQualityColor = settings.MediumQualityItemColor;
            _itemHighQualityColor = settings.HighQualityItemColor;
            _itemPerfectQualityColor = settings.PerfectQualityItemColor;

            var snowflakes = database.GetQuestItem(GameDatabase.Model.ItemId<GameDatabase.DataModel.QuestItem>.Create(25)); // TODO: add ID to settings
            if (snowflakes != null) _snowflakesColor = snowflakes.Color;
        }

        private Color ApplyRuntimeAccent(ThemeColor themeColor, Color authored)
        {
            if (!_runtimeAccentEnabled || !UsesRuntimeAccent(themeColor))
                return authored;

            Color.RGBToHSV(_runtimeAccent, out float hue, out float saturation, out float brightness);
            saturation = Mathf.Clamp01(Mathf.Max(0.18f, saturation));
            brightness = Mathf.Clamp(brightness, 0.30f, 1f);

            float targetSaturation;
            float targetBrightness;
            switch (themeColor)
            {
                case ThemeColor.BackgroundDark:
                    targetSaturation = Mathf.Lerp(0.28f, 0.58f, saturation);
                    targetBrightness = Mathf.Lerp(0.035f, 0.085f, brightness);
                    break;
                case ThemeColor.Window:
                    targetSaturation = Mathf.Lerp(0.40f, 0.76f, saturation);
                    targetBrightness = Mathf.Lerp(0.10f, 0.22f, brightness);
                    break;
                case ThemeColor.ScrollBar:
                case ThemeColor.Button:
                    targetSaturation = Mathf.Lerp(0.46f, 0.88f, saturation);
                    targetBrightness = Mathf.Lerp(0.34f, 0.64f, brightness);
                    break;
                case ThemeColor.ButtonFocus:
                case ThemeColor.Selection:
                    targetSaturation = Mathf.Lerp(0.38f, 0.78f, saturation);
                    targetBrightness = Mathf.Lerp(0.56f, 0.90f, brightness);
                    break;
                case ThemeColor.Icon:
                case ThemeColor.ButtonIcon:
                case ThemeColor.Credits:
                case ThemeColor.Tokens:
                    targetSaturation = Mathf.Lerp(0.40f, 0.82f, saturation);
                    targetBrightness = Mathf.Lerp(0.78f, 1f, brightness);
                    break;
                case ThemeColor.Text:
                case ThemeColor.ButtonText:
                    targetSaturation = Mathf.Lerp(0.22f, 0.54f, saturation);
                    targetBrightness = Mathf.Lerp(0.86f, 1f, brightness);
                    break;
                case ThemeColor.HeaderText:
                    targetSaturation = Mathf.Lerp(0.20f, 0.50f, saturation);
                    targetBrightness = Mathf.Lerp(0.90f, 1f, brightness);
                    break;
                case ThemeColor.PaleText:
                    targetSaturation = Mathf.Lerp(0.12f, 0.34f, saturation);
                    targetBrightness = Mathf.Lerp(0.72f, 0.94f, brightness);
                    break;
                case ThemeColor.BrightText:
                    targetSaturation = Mathf.Lerp(0.04f, 0.16f, saturation);
                    targetBrightness = Mathf.Lerp(0.96f, 1f, brightness);
                    break;
                default:
                    return authored;
            }

            Color themed = Color.HSVToRGB(hue, targetSaturation, targetBrightness);
            themed.a = authored.a;
            return themed;
        }

        private static bool UsesRuntimeAccent(ThemeColor themeColor)
        {
            switch (themeColor)
            {
                case ThemeColor.Window:
                case ThemeColor.ScrollBar:
                case ThemeColor.Icon:
                case ThemeColor.Selection:
                case ThemeColor.Button:
                case ThemeColor.ButtonFocus:
                case ThemeColor.ButtonText:
                case ThemeColor.ButtonIcon:
                case ThemeColor.BackgroundDark:
                case ThemeColor.Text:
                case ThemeColor.HeaderText:
                case ThemeColor.PaleText:
                case ThemeColor.BrightText:
                case ThemeColor.Credits:
                case ThemeColor.Tokens:
                    return true;
                default:
                    return false;
            }
        }

        [System.Serializable]
        public struct FontInfo
        {
            public Font Font;
            public float SizeMultiplier;
        }
    }

    public static class ColorExtension
    {
        public static Color Transparent(this Color color, float alpha) => new Color(color.r, color.g, color.b, color.a * alpha);
        public static Color Multiply(this Color color, float value) => new Color(color.r*value, color.g*value, color.b*value, color.a);

        public static Color ApplyColorMode(this Color color, ThemeColorMode colorMode)
        {
            switch (colorMode)
            {
                case ThemeColorMode.SemiTransparent25: return color.Transparent(0.25f);
                case ThemeColorMode.SemiTransparent50: return color.Transparent(0.5f);
                case ThemeColorMode.SemiTransparent75: return color.Transparent(0.75f);

                case ThemeColorMode.Brightness25: return color.Multiply(0.25f);
                case ThemeColorMode.Brightness50: return color.Multiply(0.5f);
                case ThemeColorMode.Brightbess75: return color.Multiply(0.75f);
            }

            return color;
        }
    }
}
