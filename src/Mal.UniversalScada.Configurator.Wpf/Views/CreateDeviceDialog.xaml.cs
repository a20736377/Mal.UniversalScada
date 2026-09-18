using System.Windows;
using System.Windows.Controls;
using Mal.UniversalScada.Core.Enums;
using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Configurator.Wpf.Views;

public partial class CreateDeviceDialog : Window
{
    private readonly IEnumerable<ChannelConfig> _channels;

    public DeviceNode? CreatedDevice { get; private set; }

    public CreateDeviceDialog(
        IEnumerable<ChannelConfig> channels, 
        Array protocolTypes, 
        string? defaultChannelId = null,
        int nextDeviceIndex = 1)
    {
        InitializeComponent();
        _channels = channels;

        TxtDeviceId.Text = $"DEV_{nextDeviceIndex:D2}";
        TxtDeviceName.Text = $"新建设备 {nextDeviceIndex}";

        CboChannels.ItemsSource = _channels;
        CboProtocolTypes.ItemsSource = protocolTypes;

        // 设置默认选中的通道
        if (!string.IsNullOrEmpty(defaultChannelId))
        {
            CboChannels.SelectedValue = defaultChannelId;
        }
        else
        {
            CboChannels.SelectedIndex = 0;
        }

        // 根据选中的通道自动推导初始协议类型
        UpdateDefaultProtocol();

        Loaded += (_, _) => TxtDeviceName.Focus();
    }

    private void CboChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateDefaultProtocol();
    }

    private void UpdateDefaultProtocol()
    {
        if (CboChannels.SelectedItem is ChannelConfig ch)
        {
            if (ch.ChannelType == ChannelType.SerialPort)
            {
                CboProtocolTypes.SelectedItem = ProtocolType.ModbusRtu;
            }
            else
            {
                CboProtocolTypes.SelectedItem = ProtocolType.ModbusTcp;
            }
        }
    }

    private void BtnSubmit_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = string.Empty;

        string devId = TxtDeviceId.Text.Trim();
        string devName = TxtDeviceName.Text.Trim();

        if (string.IsNullOrEmpty(devId))
        {
            TxtError.Text = "请输入设备 ID";
            TxtDeviceId.Focus();
            return;
        }

        if (string.IsNullOrEmpty(devName))
        {
            TxtError.Text = "请输入设备名称";
            TxtDeviceName.Focus();
            return;
        }

        if (CboChannels.SelectedValue is not string channelId || string.IsNullOrEmpty(channelId))
        {
            TxtError.Text = "请选择设备所属的通信通道";
            CboChannels.Focus();
            return;
        }

        if (CboProtocolTypes.SelectedItem is not ProtocolType protocolType)
        {
            TxtError.Text = "请选择通信协议类型";
            CboProtocolTypes.Focus();
            return;
        }

        if (!int.TryParse(TxtStationAddress.Text.Trim(), out int stationAddress))
        {
            stationAddress = 1;
        }

        if (!int.TryParse(TxtPollInterval.Text.Trim(), out int pollInterval) || pollInterval <= 0)
        {
            pollInterval = 100;
        }

        CreatedDevice = new DeviceNode
        {
            DeviceId = devId,
            Name = devName,
            ChannelId = channelId,
            ProtocolType = protocolType,
            StationAddress = stationAddress,
            DefaultPollIntervalMs = pollInterval,
            IsEnabled = true
        };

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
