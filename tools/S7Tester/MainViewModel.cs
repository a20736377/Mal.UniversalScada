using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7.Net;

namespace S7Tester;

/// <summary>
/// 主视图模型：负责 S7 PLC 连接测试与点位批量读取。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private Plc? _plc;

    /// <summary>可选 CPU 类型列表（绑定 ComboBox）</summary>
    public CpuType[] CpuTypes { get; } =
    {
        CpuType.S7200, CpuType.S7200Smart, CpuType.S7300,
        CpuType.S7400, CpuType.S71200, CpuType.S71500
    };

    [ObservableProperty] private string _ip = "192.168.0.1";
    [ObservableProperty] private int _rack = 0;
    [ObservableProperty] private int _slot = 1;
    [ObservableProperty] private CpuType _selectedCpuType = CpuType.S71200;

    [ObservableProperty] private string _addresses =
        "M0.0\r\nDB1.DBD0\r\nDB1.DBW4\r\nV10.0";

    [ObservableProperty] private string _statusMessage = "未连接";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReadCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReadCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool _isConnected;

    /// <summary>读取结果集合，绑定到 DataGrid</summary>
    public ObservableCollection<ReadResultItem> Results { get; } = new();

    /// <summary>连接测试：新建 Plc 并 OpenAsync，成功后保持连接供读取复用</summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        var sw = Stopwatch.StartNew();
        try
        {
            await CloseExistingAsync();
            _plc = new Plc(SelectedCpuType, Ip?.Trim() ?? string.Empty, (short)Rack, (short)Slot);
            await _plc.OpenAsync();
            sw.Stop();

            if (_plc.IsConnected)
            {
                IsConnected = true;
                StatusMessage = $"连接成功 (耗时 {sw.ElapsedMilliseconds} ms)";
            }
            else
            {
                IsConnected = false;
                StatusMessage = $"连接失败：PLC 未确认连接 (耗时 {sw.ElapsedMilliseconds} ms)";
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            IsConnected = false;
            StatusMessage = $"连接失败：{ex.Message} (耗时 {sw.ElapsedMilliseconds} ms)";
            await CloseExistingAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanConnect() => !IsBusy;

    /// <summary>批量读取：遍历地址输入框每行，逐点 ReadAsync 并填充结果表</summary>
    [RelayCommand(CanExecute = nameof(CanRead))]
    private async Task ReadAsync()
    {
        if (!IsConnected || _plc == null)
        {
            StatusMessage = "请先成功连接 PLC";
            return;
        }

        var lines = (Addresses ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
        {
            StatusMessage = "未输入任何点位地址";
            return;
        }

        Results.Clear();
        IsBusy = true;
        var sw = Stopwatch.StartNew();
        int okCount = 0, failCount = 0;

        try
        {
            foreach (var addr in lines)
            {
                try
                {
                    var value = await _plc.ReadAsync(addr);
                    Results.Add(new ReadResultItem
                    {
                        Address = addr,
                        Value = value?.ToString() ?? "(null)",
                        RawType = value?.GetType().Name ?? "null",
                        Quality = "Good",
                        Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                        Error = string.Empty
                    });
                    okCount++;
                }
                catch (Exception ex)
                {
                    Results.Add(new ReadResultItem
                    {
                        Address = addr,
                        Value = string.Empty,
                        RawType = string.Empty,
                        Quality = "Bad",
                        Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                        Error = ex.Message
                    });
                    failCount++;
                }
            }
            sw.Stop();
            StatusMessage = $"读取完成：成功 {okCount}，失败 {failCount} (耗时 {sw.ElapsedMilliseconds} ms)";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRead() => !IsBusy && IsConnected;

    /// <summary>主动断开当前连接</summary>
    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        await CloseExistingAsync();
        IsConnected = false;
        StatusMessage = "已断开连接";
    }

    private bool CanDisconnect() => !IsBusy && IsConnected;

    private async Task CloseExistingAsync()
    {
        if (_plc != null)
        {
            try { _plc.Close(); } catch { }
            _plc = null;
        }
        await Task.CompletedTask;
    }
}

/// <summary>单条读取结果，供 DataGrid 显示</summary>
public class ReadResultItem
{
    public string Address { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string RawType { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
