using System;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// 设计器点位下拉选项与元数据呈现模型（包含数据类型、描述、工程单位、物理地址与组件兼容性判断）
/// </summary>
public class TagOptionItem
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TagDataType? DataType { get; set; }
    public string DataTypeLabel { get; set; } = "未绑定";
    public string Unit { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public TagAccessMode AccessMode { get; set; } = TagAccessMode.ReadOnly;
    public string DisplayText { get; set; } = string.Empty;

    public bool IsNumeric => DataType is TagDataType.Float or TagDataType.Double
        or TagDataType.Int8 or TagDataType.UInt8
        or TagDataType.Int16 or TagDataType.UInt16
        or TagDataType.Int32 or TagDataType.UInt32
        or TagDataType.Int64 or TagDataType.UInt64;

    public bool IsFloat => DataType is TagDataType.Float or TagDataType.Double;
    public bool IsInteger => IsNumeric && !IsFloat;
    public bool IsBool => DataType == TagDataType.Bool;
    public bool IsWordOrInteger => DataType is TagDataType.UInt8 or TagDataType.Int8 or TagDataType.UInt16 or TagDataType.Int16 or TagDataType.UInt32 or TagDataType.Int32;

    public string DetailSummary => $"地址: {(string.IsNullOrWhiteSpace(Address) ? "内部变量" : Address)} | 单位: {(string.IsNullOrWhiteSpace(Unit) ? "--" : Unit)} | 权限: {AccessMode}";

    /// <summary>
    /// 强校验组件类型与点位数据类型的匹配规则（保证“只能选择对应的点位”）
    /// </summary>
    public static bool IsCompatibleWithWidget(WidgetType widgetType, TagDataType dataType, TagAccessMode accessMode = TagAccessMode.ReadOnly)
    {
        bool isNumeric = dataType is TagDataType.Float or TagDataType.Double
            or TagDataType.Int8 or TagDataType.UInt8
            or TagDataType.Int16 or TagDataType.UInt16
            or TagDataType.Int32 or TagDataType.UInt32
            or TagDataType.Int64 or TagDataType.UInt64;

        bool isBool = dataType == TagDataType.Bool;
        bool isWordOrInteger = dataType is TagDataType.UInt8 or TagDataType.Int8
            or TagDataType.UInt16 or TagDataType.Int16
            or TagDataType.UInt32 or TagDataType.Int32;

        return widgetType switch
        {
            WidgetType.GaugeCircular or WidgetType.GaugeArc or WidgetType.LevelTank or WidgetType.NumericCard or WidgetType.TrendChart
                => isNumeric,
            WidgetType.StatusLed or WidgetType.Valve or WidgetType.Pump
                => isBool,
            WidgetType.Pipe
                => isBool || isNumeric,
            WidgetType.IoMatrix
                => isWordOrInteger,
            WidgetType.ControlButton
                => isBool || (isNumeric && accessMode != TagAccessMode.ReadOnly),
            WidgetType.SetpointInput
                => isNumeric && accessMode != TagAccessMode.ReadOnly,
            WidgetType.DisplayBox
                => isNumeric || isBool || dataType == TagDataType.String,
            WidgetType.TextLabel or WidgetType.PanelContainer or WidgetType.Image or WidgetType.DeviceStatus
                => false, // 纯静态展示标签与容器组件，无需绑定采集点位
            _ => true
        };
    }

    public static TagOptionItem FromTagNode(TagNode tag)
    {
        string typeLabel = tag.DataType switch
        {
            TagDataType.Bool => "Bool 开关量",
            TagDataType.Int8 => "Int8 字节",
            TagDataType.UInt8 => "UInt8 无符号字节",
            TagDataType.Int16 => "Int16 16位整型",
            TagDataType.UInt16 => "UInt16 状态字",
            TagDataType.Int32 => "Int32 32位整型",
            TagDataType.UInt32 => "UInt32 双字",
            TagDataType.Int64 => "Int64 64位整型",
            TagDataType.UInt64 => "UInt64 64位无符号",
            TagDataType.Float => "Float 浮点数",
            TagDataType.Double => "Double 双精度",
            TagDataType.String => "String 字符串",
            TagDataType.ByteArray => "Bytes 字节数组",
            _ => tag.DataType.ToString()
        };

        string unitText = string.IsNullOrWhiteSpace(tag.Unit) ? string.Empty : $" ({tag.Unit})";
        string descText = string.IsNullOrWhiteSpace(tag.Name) ? string.Empty : $" - {tag.Name}";
        string display = $"[#{tag.Id}] [{typeLabel}]{descText}{unitText}";

        return new TagOptionItem
        {
            Id = tag.Id,
            Name = tag.Name,
            DataType = tag.DataType,
            DataTypeLabel = typeLabel,
            Unit = tag.Unit,
            Address = tag.Address,
            AccessMode = tag.AccessMode,
            DisplayText = display
        };
    }

    public static TagOptionItem CreateUnbound() => new()
    {
        Id = 0,
        Name = "未绑定点位",
        DataType = null,
        DataTypeLabel = "未绑定",
        Unit = string.Empty,
        Address = string.Empty,
        DisplayText = "-- (未绑定点位 / 静态演示) --"
    };

    public static TagOptionItem CreateFallback(long tagId)
    {
        return new TagOptionItem
        {
            Id = tagId,
            Name = $"点位 #{tagId}",
            DataType = TagDataType.Float,
            DataTypeLabel = "Float 浮点数",
            DisplayText = $"[⚠️已失效/未入库] 点位 #{tagId}"
        };
    }
}
