using System.Globalization;
using System.IO;
using System.Text;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Configuration;

/// <summary>
/// 点位组态导入导出服务接口
/// </summary>
public interface ITagImportExportService
{
    /// <summary>
    /// 将点位集合导出为 CSV 文件
    /// </summary>
    Task ExportToCsvAsync(IEnumerable<TagNode> tags, string filePath);

    /// <summary>
    /// 从 CSV 文件解析并导入点位
    /// </summary>
    Task<IReadOnlyList<TagNode>> ImportFromCsvAsync(string filePath, string? defaultDeviceId = null);
}

/// <summary>
/// 标准 CSV 格式点位导入导出实现
/// </summary>
public class CsvTagImportExportService : ITagImportExportService
{
    private const string Header = "Id,DeviceId,Name,Address,DataType,AccessMode,ScaleFactor,Offset,Unit,Deadband,ScanIntervalMs,IsHistorical";

    public async Task ExportToCsvAsync(IEnumerable<TagNode> tags, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var tag in tags)
        {
            var line = $"{tag.Id},{Escape(tag.DeviceId)},{Escape(tag.Name)},{Escape(tag.Address)}," +
                       $"{tag.DataType},{tag.AccessMode},{tag.ScaleFactor.ToString(CultureInfo.InvariantCulture)}," +
                       $"{tag.Offset.ToString(CultureInfo.InvariantCulture)},{Escape(tag.Unit)}," +
                       $"{tag.Deadband.ToString(CultureInfo.InvariantCulture)},{tag.ScanIntervalMs},{tag.IsHistorical}";
            sb.AppendLine(line);
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    public async Task<IReadOnlyList<TagNode>> ImportFromCsvAsync(string filePath, string? defaultDeviceId = null)
    {
        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
        var result = new List<TagNode>();

        if (lines.Length <= 1) return result;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = ParseCsvLine(line);
            if (parts.Count < 4) continue;

            long id = long.TryParse(parts[0], out var parsedId) ? parsedId : 0;
            string deviceId = !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : (defaultDeviceId ?? "DEV_DEFAULT");
            string name = parts[2];
            string address = parts[3];

            var tag = new TagNode
            {
                Id = id,
                DeviceId = deviceId,
                Name = name,
                Address = address,
                DataType = parts.Count > 4 && Enum.TryParse<TagDataType>(parts[4], out var dt) ? dt : TagDataType.Int16,
                AccessMode = parts.Count > 5 && Enum.TryParse<TagAccessMode>(parts[5], out var am) ? am : TagAccessMode.ReadWrite,
                ScaleFactor = parts.Count > 6 && double.TryParse(parts[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var sf) ? sf : 1.0,
                Offset = parts.Count > 7 && double.TryParse(parts[7], NumberStyles.Any, CultureInfo.InvariantCulture, out var off) ? off : 0.0,
                Unit = parts.Count > 8 ? parts[8] : "",
                Deadband = parts.Count > 9 && double.TryParse(parts[9], NumberStyles.Any, CultureInfo.InvariantCulture, out var db) ? db : 0.0,
                ScanIntervalMs = parts.Count > 10 && int.TryParse(parts[10], out var scan) ? scan : 100,
                IsHistorical = parts.Count <= 11 || !bool.TryParse(parts[11], out var hist) || hist
            };

            result.Add(tag);
        }

        return result;
    }

    private static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }
        return text;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString().Trim());
        return result;
    }
}
