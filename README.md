# Mal.UniversalScada — Configurable SCADA/HMI for Heterogeneous PLCs

An industrial-grade data acquisition and monitoring (SCADA) platform built on .NET 8 + WPF. Supports Modbus, Siemens S7, and custom serial protocols for multi-brand PLC integration. Provides a drag-and-drop configuration designer and runtime monitoring UI, with a write-priority preemptive scheduling engine to guarantee real-time control command delivery.

> **License:** This project uses a [Non-Commercial License](LICENSE) for personal study, academic research, and non-commercial use only. Contact the copyright holder for commercial licensing.

> **中文文档：** [README.zh-CN.md](README.zh-CN.md)

---

## Table of Contents

- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Core Capabilities](#core-capabilities)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Supported Protocols & Address Formats](#supported-protocols--address-formats)
- [S7Tester Tool](#s7tester-tool)
- [Design Documents](#design-documents)
- [FAQ](#faq)

---

## Tech Stack

| Category | Technology |
|----------|-----------|
| Runtime | .NET 8 (net8.0 / net8.0-windows) |
| UI Framework | WPF + MVVM (CommunityToolkit.Mvvm) |
| Data Storage | SQLite (Microsoft.Data.Sqlite, WAL mode) |
| Async Queues | System.Threading.Channels |
| ORM | Dapper |
| DI | Microsoft.Extensions.DependencyInjection |
| Test Framework | xUnit |

---

## Project Structure

```
d:\GHG\AUTO\
├── Mal.UniversalScada.sln               # Solution file
├── LICENSE                               # Non-commercial license (English)
├── LICENSE.zh-CN                         # Non-commercial license (Chinese)
├── README.md                             # This document (English)
├── README.zh-CN.md                       # Chinese README
├── .gitignore
│
├── src/                                  # Source code
│   ├── Mal.UniversalScada.Core/          # Core: abstractions, scheduler, data bus, alarms
│   ├── Mal.UniversalScada.Drivers.Modbus/        # Modbus driver (RTU/ASCII/TCP)
│   ├── Mal.UniversalScada.Drivers.Siemens/       # Siemens S7 driver
│   ├── Mal.UniversalScada.Drivers.CustomSerial/  # Custom serial protocol driver
│   ├── Mal.UniversalScada.Storage.Sqlite/        # SQLite config/history/user storage
│   ├── Mal.UniversalScada.UI.Wpf/                # Runtime monitoring UI (WinExe)
│   ├── Mal.UniversalScada.Configurator.Wpf/      # Drag-and-drop configuration designer (WinExe)
│   └── Mal.UniversalScada.UI.Controls/           # Industrial widget library
│
├── tests/                                # Unit/integration tests
│   ├── Mal.UniversalScada.Core.Tests/
│   ├── Mal.UniversalScada.Drivers.Modbus.Tests/
│   ├── Mal.UniversalScada.Drivers.Siemens.Tests/
│   └── Mal.UniversalScada.Drivers.CustomSerial.Tests/
│
├── tools/
│   └── S7Tester/                         # Standalone S7 connection test tool (WinExe)
│
└── document/                             # Design documents (Chinese)
```

---

## Core Capabilities

### 1. Write-Priority Preemptive Scheduling

- Each physical channel runs an independent worker thread, isolated from others
- Write commands are enqueued via an unbounded `Channel<WriteJob>` and always execute before periodic read polling
- Two preemption checkpoints: drain the write queue at loop start + re-check before each device read
- See [Write-Priority Scheduler Implementation Details](document/写优先插队调度器实现逻辑.md) (Chinese)

### 2. Real-Time Data Bus

- In-memory snapshot model — reads never hit the database
- Deadband filtering suppresses trivial value-change notifications
- Pub/sub pattern; UI widgets subscribe to tags on demand

### 3. Multi-Protocol Drivers

- **Modbus**: RTU / ASCII / TCP transport modes, CRC/LRC validation, configurable endianness
- **Siemens S7**: S7-200/300/400/1200/1500/200Smart, COTP handshake, PDU encode/decode
- **Custom Serial**: Configurable frame header/footer/checksum, adaptable to non-standard devices

### 4. Alarm Engine

- Multi-level threshold evaluation (high / high-high / low / low-low)
- Alarm acknowledgement and recovery mechanism
- Audio/visual integration (bindable to UI controls)

### 5. Configuration Designer

- Drag-and-drop canvas for industrial widget layout
- Widget properties bind to tag addresses
- Multi-screen / multi-view projection

### 6. Industrial Widget Library

| Widget | Purpose |
|--------|---------|
| PumpControl | Pump running/stopped/fault status |
| ValveControl | Valve open/closed/fault |
| PipeControl | Pipe fluid animation |
| TankLevelControl | Tank level display |
| TrendChartControl | Real-time trend chart |
| CircularGaugeControl | Circular gauge dial |
| NumericCardControl | Numeric value card |
| StatusLedControl | Status indicator LED |
| IoMatrixControl | IO matrix overview |
| AlarmBannerControl | Scrolling alarm banner |
| ControlButtonControl | Control command button |
| DisplayBoxControl | Data display box |
| TextLabelControl | Text label |
| DeviceStatusControl | Device composite status |
| PanelContainerControl | Container panel |

---

## Prerequisites

- **OS**: Windows 10/11 (x64)
- **.NET SDK**: .NET 8.0+ ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- **IDE** (optional): Visual Studio 2022 17.8+ or JetBrains Rider
- **PLC-side requirements**:
  - Siemens: CPU must enable "Permit access with PUT/GET communication"
  - DB blocks must use standard access (disable "Optimized block access")

---

## Quick Start

### 1. Clone & Build

```shell
git clone <repo-url>
cd AUTO
dotnet build Mal.UniversalScada.sln -c Release
```

### 2. Launch Configuration Designer

```shell
dotnet run --project src/Mal.UniversalScada.Configurator.Wpf -c Release
```

Configure channels, devices, and tags in the designer, then save to the SQLite database (default `scada_config.db`).

### 3. Launch Runtime Monitoring

```shell
dotnet run --project src/Mal.UniversalScada.UI.Wpf -c Release
```

The runtime app auto-loads the database config, connects to PLCs, and starts real-time acquisition.

### 4. Run Tests

```shell
dotnet test Mal.UniversalScada.sln
```

---

## Configuration

System configuration is stored in a SQLite database with the following main tables:

| Table | Contents |
|-------|----------|
| `Channels` | Communication channels (TCP/serial parameters) |
| `Devices` | PLC devices (protocol type, station address, rack/slot) |
| `Tags` | Data points (address, data type, deadband, poll interval) |
| `UiViews` | Configured screens (widget type, position, tag binding) |

### Channel Configuration Example

| Field | Value | Description |
|-------|-------|-------------|
| ChannelType | TcpClient | TCP client |
| Host | 192.168.0.1 | PLC IP address |
| Port | 102 | Default S7 protocol port |

### Device Configuration Example (Siemens S7-1200)

| Field | Value |
|-------|-------|
| ProtocolType | SiemensS7 |
| CustomProtocolName | `Cpu=S71200;Rack=0;Slot=1` |
| StationAddress | 0 |
| DefaultPollIntervalMs | 1000 |

### Tag Configuration Example

| Field | Value |
|-------|-------|
| Address | `DB1.DBX0.0` |
| DataType | Bool |
| Deadband | 0 |
| Description | Start Button |

---

## Supported Protocols & Address Formats

### Siemens S7

| Memory Area | Format | Example | Description |
|-------------|--------|---------|-------------|
| DB (bit) | `DB{n}.DBX{byte}.{bit}` | `DB1.DBX0.0` | DB1 byte 0, bit 0 |
| DB (byte) | `DB{n}.DBB{byte}` | `DB1.DBB0` | DB1 byte 0 |
| DB (word) | `DB{n}.DBW{byte}` | `DB1.DBW0` | DB1 word 0 (2 bytes) |
| DB (dword) | `DB{n}.DBD{byte}` | `DB1.DBD0` | DB1 dword 0 (4 bytes) |
| M (bit) | `M{byte}.{bit}` | `M0.0` | Marker memory |
| I (bit) | `I{byte}.{bit}` | `I0.0` | Input image |
| Q (bit) | `Q{byte}.{bit}` | `Q0.0` | Output image |

> Shorthand: `DB1.0.0` is equivalent to `DB1.DBX0.0`.

### Modbus

| Data Area | Format | Example |
|-----------|--------|---------|
| Coils | `{addr}` | `100` |
| Discrete Input | `I{addr}` | `I100` |
| Input Register | `IR{addr}` | `IR100` |
| Holding Register | `HR{addr}` | `HR100` |

> Word order (big-endian / little-endian / word-swap) is configurable per device.

---

## S7Tester Tool

A standalone S7 protocol connection testing tool with no dependency on the main project. Used to verify PLC connectivity and tag read/write.

### Launch

```shell
cd tools/S7Tester
dotnet run
```

### Usage

1. Enter PLC IP address, rack (S7-1200 default: 0), slot (S7-1200 default: 1), and CPU type
2. Click "Connect Test" to verify communication
3. Enter tag addresses in the text box (one per line, e.g. `DB1.DBX0.0`)
4. Click "Read Data" to retrieve tag values

### Self-Contained Publishing

No .NET 8 runtime required on the target machine:

```shell
dotnet publish tools/S7Tester/S7Tester.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output is in `tools/S7Tester/bin/Release/net8.0-windows/win-x64/publish/`.

---

## Design Documents

All design documents are in Chinese:

| Document | Content |
|----------|---------|
| [Architecture Design](document/通用上位机管理软件架构设计方案.md) | Layered architecture, module breakdown |
| [.NET 8 Multi-Screen Design](document/NET8上位机代码架构与多屏设计方案.md) | .NET 8 tech selection, multi-screen projection |
| [Configurable SCADA UI](document/异构下位机可配置SCADA界面架构设计方案.md) | Configurable UI, widget system |
| [Feature Roadmap](document/系统待开发功能规划与路线图.md) | Feature planning and milestones |
| [Write-Priority Scheduler](document/写优先插队调度器实现逻辑.md) | Deep dive into the scheduling engine |

---

## FAQ

### Q: The application starts but no window appears?

Ensure the target machine has the .NET 8 Desktop Runtime (`Microsoft.WindowsDesktop.App 8.0`) installed. WinExe projects exit silently when the runtime is missing. Use self-contained publishing to avoid this.

### Q: S7 connection reports "TPKT is incomplete / invalid"?

TCP connected but protocol handshake failed. Check in priority order:

1. PLC has not enabled "Permit access with PUT/GET communication" (TIA Portal → CPU Properties → Protection & Security)
2. Using PLCSIM Basic (does not support external S7 connections); use PLCSIM Advanced instead
3. Rack/slot mismatch (S7-1200 is always Rack=0, Slot=1)
4. PLC connection resources exhausted

### Q: DB block address offsets are all empty?

If the DB block has "Optimized block access" enabled in TIA Portal, offsets will not be shown. Disable it in the DB properties, switch to standard access mode, and re-download.

### Q: Serial device communication is unstable?

Verify baud rate, data bits, stop bits, and parity match the device. For custom serial protocols, confirm frame header/footer/checksum configuration is correct.

---

## License

This project uses a [Non-Commercial License](LICENSE). Read the full license before use.

- **Allowed**: Personal study, academic research, educational use
- **Prohibited**: Any form of commercial use
- **Commercial licensing**: Contact the copyright holder
