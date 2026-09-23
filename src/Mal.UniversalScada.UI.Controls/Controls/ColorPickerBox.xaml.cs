using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Mal.UniversalScada.UI.Controls.Controls;

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
        {
            hex = "#0284C7";
        }

        if (!hex.StartsWith('#'))
        {
            hex = "#" + hex;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            _isUpdatingInternally = true;

            var brush = new SolidColorBrush(color);
            ColorPreviewBorder.Background = brush;
            PopupCurrentPreview.Background = brush;
            PopupHexLabel.Text = hex.ToUpperInvariant();

            if (HexTextBox.Text != hex.ToUpperInvariant())
            {
                HexTextBox.Text = hex.ToUpperInvariant();
            }

            RSlider.Value = color.R;
            GSlider.Value = color.G;
            BSlider.Value = color.B;

            RValueLabel.Text = color.R.ToString();
            GValueLabel.Text = color.G.ToString();
            BValueLabel.Text = color.B.ToString();
        }
        catch
        {
            // 输入格式暂不合法时保持输入框编辑，不抛异常
        }
        finally
        {
            _isUpdatingInternally = false;
        }
    }

    private void OnTogglePopupClick(object sender, RoutedEventArgs e)
    {
        PickerPopup.IsOpen = !PickerPopup.IsOpen;
    }

    private void OnHexTextBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInternally) return;

        var text = HexTextBox.Text?.Trim() ?? string.Empty;
        if (!text.StartsWith('#'))
        {
            text = "#" + text;
        }

        if (text.Length == 7 || text.Length == 9)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(text);
                _isUpdatingInternally = true;
                ColorHex = text.ToUpperInvariant();
                var brush = new SolidColorBrush(color);
                ColorPreviewBorder.Background = brush;
                PopupCurrentPreview.Background = brush;
                PopupHexLabel.Text = ColorHex;

                RSlider.Value = color.R;
                GSlider.Value = color.G;
                BSlider.Value = color.B;
                RValueLabel.Text = color.R.ToString();
                GValueLabel.Text = color.G.ToString();
                BValueLabel.Text = color.B.ToString();
            }
            catch
            {
                // 忽略解析错误
            }
            finally
            {
                _isUpdatingInternally = false;
            }
        }
    }

    private void OnPresetColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            ApplyColor(hex);
        }
    }

    private void OnRgbSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingInternally) return;

        byte r = (byte)Math.Clamp(RSlider.Value, 0, 255);
        byte g = (byte)Math.Clamp(GSlider.Value, 0, 255);
        byte b = (byte)Math.Clamp(BSlider.Value, 0, 255);

        RValueLabel.Text = r.ToString();
        GValueLabel.Text = g.ToString();
        BValueLabel.Text = b.ToString();

        var hex = $"#{r:X2}{g:X2}{b:X2}";
        ApplyColor(hex);
    }

    private void ApplyColor(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            _isUpdatingInternally = true;

            ColorHex = hex.ToUpperInvariant();
            HexTextBox.Text = ColorHex;
            PopupHexLabel.Text = ColorHex;

            var brush = new SolidColorBrush(color);
            ColorPreviewBorder.Background = brush;
            PopupCurrentPreview.Background = brush;

            RSlider.Value = color.R;
            GSlider.Value = color.G;
            BSlider.Value = color.B;
            RValueLabel.Text = color.R.ToString();
            GValueLabel.Text = color.G.ToString();
            BValueLabel.Text = color.B.ToString();
        }
        catch
        {
            // 忽略异常
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
            var currentHex = ColorHex;
            int initialRgb = 0x0284C7;
            try
            {
                var curColor = (Color)ColorConverter.ConvertFromString(currentHex);
                // Win32 COLORREF format is 0x00bbggrr
                initialRgb = curColor.R | (curColor.G << 8) | (curColor.B << 16);
            }
            catch
            {
            }

            var cc = new CHOOSECOLOR();
            cc.lStructSize = Marshal.SizeOf(typeof(CHOOSECOLOR));
            cc.hwndOwner = (HwndSource.FromVisual(this) as HwndSource)?.Handle ?? IntPtr.Zero;
            cc.rgbResult = initialRgb;
            cc.lpCustColors = _customColorsHandle.AddrOfPinnedObject();
            cc.Flags = CC_RGBINIT | CC_FULLOPEN;

            if (ChooseColor(ref cc))
            {
                byte r = (byte)(cc.rgbResult & 0xFF);
                byte g = (byte)((cc.rgbResult >> 8) & 0xFF);
                byte b = (byte)((cc.rgbResult >> 16) & 0xFF);
                var pickedHex = $"#{r:X2}{g:X2}{b:X2}";
                ApplyColor(pickedHex);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"调出系统调色盘失败: {ex.Message}", "调色盘错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
