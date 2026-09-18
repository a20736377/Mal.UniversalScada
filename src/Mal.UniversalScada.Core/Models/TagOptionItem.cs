using System;
using Mal.UniversalScada.Core.Enums;

namespace Mal.UniversalScada.Core.Models;

/// <summary>
/// 设计器点位下拉选项与元数据呈现模型（包含数据类型、描述、工程单位、物理地址与组件兼容性判断）
/// </summary>
public class TagOptionItem
{
    public string TagId { get; set; } = string.Empty;
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
            WidgetType.GaugeCircular or WidgetType.LevelTank or WidgetType.NumericCard or WidgetType.TrendChart
                => isNumeric,
            WidgetType.StatusLed
                => isBool,
            WidgetType.IoMatrix
                => isWordOrInteger,
            WidgetType.ControlButton
                => isBool || (isNumeric && accessMode != TagAccessMode.ReadOnly),
            WidgetType.SetpointInput
                => isNumeric && accessMode != TagAccessMode.ReadOnly,
            WidgetType.DisplayBox
                => isNumeric || isBool || dataType == TagDataType.String,
            WidgetType.TextLabel
                => true,
            WidgetType.PanelContainer
                => false, // 容器组件为纯布局分组框，无需绑定采集点位
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
        string display = $"[{typeLabel}] {tag.TagId}{descText}{unitText}";

        return new TagOptionItem
        {
            TagId = tag.TagId,
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
        TagId = string.Empty,
        Name = "未绑定点位",
        DataType = null,
        DataTypeLabel = "未绑定",
        Unit = string.Empty,
        Address = string.Empty,
        DisplayText = "-- (未绑定点位 / 静态演示) --"
    };

    public static TagOptionItem CreateFallback(string tagId)
    {
        bool isBool = tagId.Contains("Status", StringComparison.OrdinalIgnoreCase) ||
                      tagId.Contains("Cmd", StringComparison.OrdinalIgnoreCase) ||
                      tagId.Contains("Start", StringComparison.OrdinalIgnoreCase) ||
                      tagId.Contains("Stop", StringComparison.OrdinalIgnoreCase);

        bool isWord = tagId.Contains("Word", StringComparison.OrdinalIgnoreCase) ||
                      tagId.Contains("Matrix", StringComparison.OrdinalIgnoreCase) ||
                      tagId.Contains("IO", StringComparison.OrdinalIgnoreCase);

        TagDataType type = isBool ? TagDataType.Bool : (isWord ? TagDataType.UInt16 : TagDataType.Float);
        string label = isBool ? "Bool 开关量" : (isWord ? "UInt16 状态字" : "Float 浮点数");

        return new TagOptionItem
        {
            TagId = tagId,
            Name = tagId,
            DataType = type,
            DataTypeLabel = label,
            DisplayText = $"[⚠️已失效/未入库] {tagId} ({label})"
        };
    }
}
