using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.PropertyEditors;

public partial class ImagePropertyEditor : UserControl
{
    public ImagePropertyEditor()
    {
        InitializeComponent();
    }

    private void OnBrowseImageClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择工控图片文件",
            Filter = "常见图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.svg;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.svg;*.ico|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            if (DataContext is ImageWidgetViewModel ivm)
            {
                ivm.Props.ImagePath = dialog.FileName;
            }
            else if (DataContext is WidgetViewModel wvm && wvm.ImageProps != null)
            {
                wvm.ImageProps.ImagePath = dialog.FileName;
            }
        }
    }
}
