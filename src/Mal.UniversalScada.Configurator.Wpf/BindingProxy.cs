using System.Windows;

namespace Mal.UniversalScada.Configurator.Wpf;

/// <summary>
/// 解决 WPF 在 ContextMenu、Popup 等脱离主视觉树的元素中无法跨树寻找 Ancestor DataContext 的数据代理类
/// </summary>
public class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();

    public object Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
}
