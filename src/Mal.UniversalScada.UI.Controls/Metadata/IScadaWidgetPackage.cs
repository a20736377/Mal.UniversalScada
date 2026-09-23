namespace Mal.UniversalScada.UI.Controls.Metadata;

/// <summary>
/// SCADA 工业组件扩展包包契约。
/// 外部类库（如 ThirdParty.Widgets.dll）可实现此接口，在程序集载入或插件扫描时被 WidgetRegistry 统一发现并触发初始化。
/// </summary>
public interface IScadaWidgetPackage
{
    /// <summary>
    /// 组件包唯一标识名称
    /// </summary>
    string PackageName { get; }

    /// <summary>
    /// 组件包版本
    /// </summary>
    string Version => "1.0.0";

    /// <summary>
    /// 组件包提供者 / 作者
    /// </summary>
    string Author => "SCADA Extension";

    /// <summary>
    /// 初始化包，向注册中心注册自定义预设、服务或组件元数据
    /// </summary>
    /// <param name="registry">组件注册中心单例</param>
    void Initialize(WidgetRegistry registry);
}
