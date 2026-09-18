using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Mal.UniversalScada.Configurator.Wpf.Controls;

/// <summary>
/// 工业可视化高颜值颜色拾取器控件（支持色块预览、Hex手动输入、经典工业预设色板、RGB滑块微调及原生系统调色盘）
/// </summary>
public partial class ColorPickerBox : UserControl
{
    private bool _isUpdatingInternally;

    // Win32 ChooseColor interop for native Windows color dialog without WinForms dependencies
    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool ChooseColor(ref CHOOSECOLOR cc);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct CHOOSECOLOR
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public int rgbResult;
        public IntPtr lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    private const int CC_RGBINIT = 0x00000001;
    private const int CC_FULLOPEN = 0x00000002;
    private static readonly int[] _customColors = new int[16];
    private static readonly GCHandle _customColorsHandle = GCHandle.Alloc(_customColors, GCHandleType.Pinned);

    public static readonly DependencyProperty ColorHexProperty =
        DependencyProperty.Register(
            nameof(ColorHex),
            typeof(string),
            typeof(ColorPickerBox),
            new FrameworkPropertyMetadata("#0284C7", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorHexPropertyChanged));

    public string ColorHex
    {
        get => (string)GetValue(ColorHexProperty);
        set => SetValue(ColorHexProperty, value);
    }

    public ColorPickerBox()
    {
        InitializeComponent();
        UpdateVisuals(ColorHex);
    }

    private static void OnColorHexPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerBox box && !box._isUpdatingInternally)
        {
            box.UpdateVisuals(e.NewValue as string);
        }
    }

    private void UpdateVisuals(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            hex = "#0284C7";

        var color = ParseColor(hex);
        var brush = new SolidColorBrush(color);

        _isUpdatingInternally = true;
        try
        {
            if (HexTextBox.Text != hex)
            {
                HexTextBox.Text = hex;
            }

            ColorPreviewBorder.Background = brush;
            PopupCurrentPreview.Background = brush;
            PopupHexLabel.Text = hex.ToUpperInvariant();

            RSlider.Value = color.R;
            GSlider.Value = color.G;
            BSlider.Value = color.B;

            RValueLabel.Text = color.R.ToString();
            GValueLabel.Text = color.G.ToString();
            BValueLabel.Text = color.B.ToString();
        }
        finally
        {
            _isUpdatingInternally = false;
        }
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            hex = hex.Trim();
            if (!hex.StartsWith("#"))
                hex = "#" + hex;

            if (ColorConverter.ConvertFromString(hex) is Color c)
                return c;
        }
        catch
        {
            // fallback
        }
        return Color.FromRgb(2, 132, 199);
    }

    private void OnTogglePopupClick(object sender, RoutedEventArgs e)
    {
        PickerPopup.IsOpen = !PickerPopup.IsOpen;
    }

    private void OnHexTextBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInternally)
            return;

        var text = HexTextBox.Text?.Trim() ?? string.Empty;
        if (text.Length is 4 or 7 or 9)
        {
            try
            {
                var color = ParseColor(text);
                var formattedHex = text.StartsWith("#") ? text : "#" + text;

                _isUpdatingInternally = true;
                try
                {
                    ColorHex = formattedHex;
                    var brush = new SolidColorBrush(color);
                    ColorPreviewBorder.Background = brush;
                    PopupCurrentPreview.Background = brush;
                    PopupHexLabel.Text = formattedHex.ToUpperInvariant();

                    RSlider.Value = color.R;
                    GSlider.Value = color.G;
                    BSlider.Value = color.B;

                    RValueLabel.Text = color.R.ToString();
                    GValueLabel.Text = color.G.ToString();
                    BValueLabel.Text = color.B.ToString();
                }
                finally
                {
                    _isUpdatingInternally = false;
                }
            }
            catch
            {
                // ignore transient typing
            }
        }
    }

    private void OnPresetColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            ApplyColor(hex);
            PickerPopup.IsOpen = false;
        }
    }

    private void OnRgbSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingInternally)
            return;

        byte r = (byte)Math.Clamp(RSlider.Value, 0, 255);
        byte g = (byte)Math.Clamp(GSlider.Value, 0, 255);
        byte b = (byte)Math.Clamp(BSlider.Value, 0, 255);

        string hex = $"#{r:X2}{g:X2}{b:X2}";
        ApplyColor(hex);
    }

    private void ApplyColor(string hex)
    {
        _isUpdatingInternally = true;
        try
        {
            ColorHex = hex;
            var color = ParseColor(hex);
            var brush = new SolidColorBrush(color);

            HexTextBox.Text = hex;
            ColorPreviewBorder.Background = brush;
            PopupCurrentPreview.Background = brush;
            PopupHexLabel.Text = hex.ToUpperInvariant();

            RSlider.Value = color.R;
            GSlider.Value = color.G;
            BSlider.Value = color.B;

            RValueLabel.Text = color.R.ToString();
            GValueLabel.Text = color.G.ToString();
            BValueLabel.Text = color.B.ToString();
        }
        finally
        {
            _isUpdatingInternally = false;
        }
    }

    private void OnOpenSystemColorDialogClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var currentColor = ParseColor(ColorHex);
            int initRgb = currentColor.R | (currentColor.G << 8) | (currentColor.B << 16);

            var window = Window.GetWindow(this);
            var hwnd = window != null ? new WindowInteropHelper(window).Handle : IntPtr.Zero;

            var cc = new CHOOSECOLOR
            {
                lStructSize = Marshal.SizeOf<CHOOSECOLOR>(),
                hwndOwner = hwnd,
                rgbResult = initRgb,
                lpCustColors = _customColorsHandle.AddrOfPinnedObject(),
                Flags = CC_RGBINIT | CC_FULLOPEN
            };

            if (ChooseColor(ref cc))
            {
                byte r = (byte)(cc.rgbResult & 0xFF);
                byte g = (byte)((cc.rgbResult >> 8) & 0xFF);
                byte b = (byte)((cc.rgbResult >> 16) & 0xFF);
                string hex = $"#{r:X2}{g:X2}{b:X2}";
                ApplyColor(hex);
                PickerPopup.IsOpen = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ChooseColor error: {ex.Message}");
        }
    }
}
