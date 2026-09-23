# Mal.UniversalScada — 异构下位机可配置 SCADA/HMI 上位机系统

基于 .NET 8 + WPF 的工业级数据采集与监控（SCADA）平台，支持 Modbus、Siemens S7、自定义串口协议等多品牌 PLC 接入，提供组态设计器与运行时监控界面，采用写优先插队调度引擎保障控制指令实时下发。

> **许可声明：** 本项目采用 [非商用开源许可](LICENSE)，仅供个人学习、学术研究和非商业用途使用。商用请联系版权持有人获取授权。

---

## 目录

- [技术栈](#技术栈)
- [项目结构](#项目结构)
- [核心能力](#核心能力)
- [环境要求](#环境要求)
- [快速开始](#快速开始)
- [配置说明](#配置说明)
- [支持的协议与地址格式](#支持的协议与地址格式)
- [S7Tester 工具](#s7tester-工具)
- [设计文档](#设计文档)
- [常见问题](#常见问题)

---

## 技术栈

| 类别 | 技术选型 |
|------|---------|
| 运行时 | .NET 8 (net8.0 / net8.0-windows) |
| UI 框架 | WPF + MVVM (CommunityToolkit.Mvvm) |
| 数据存储 | SQLite (Microsoft.Data.Sqlite, WAL 模式) |
| 异步队列 | System.Threading.Channels |
| ORM | Dapper |
| 依赖注入 | Microsoft.Extensions.DependencyInjection |
| 测试框架 | xUnit |

---

## 项目结构

```
d:\GHG\AUTO\
├── Mal.UniversalScada.sln               # 解决方案文件
├── LICENSE                               # 非商用许可协议
├── README.md                             # 本文档
├── .gitignore
│
├── src/                                  # 源代码
│   ├── Mal.UniversalScada.Core/          # 核心层：抽象、调度、数据总线、报警引擎
│   ├── Mal.UniversalScada.Drivers.Modbus/        # Modbus 驱动 (RTU/ASCII/TCP)
│   ├── Mal.UniversalScada.Drivers.Siemens/       # Siemens S7 驱动
│   ├── Mal.UniversalScada.Drivers.CustomSerial/  # 自定义串口协议驱动
│   ├── Mal.UniversalScada.Storage.Sqlite/        # SQLite 配置/历史/用户存储
│   ├── Mal.UniversalScada.UI.Wpf/                # 运行时监控界面 (WinExe)
│   ├── Mal.UniversalScada.Configurator.Wpf/      # 拖拽式组态设计器 (WinExe)
│   └── Mal.UniversalScada.UI.Controls/           # 工业控件库 (泵/阀/管道/趋势图等)
│
├── tests/                                # 单元/集成测试
│   ├── Mal.UniversalScada.Core.Tests/
│   ├── Mal.UniversalScada.Drivers.Modbus.Tests/
│   ├── Mal.UniversalScada.Drivers.Siemens.Tests/
│   └── Mal.UniversalScada.Drivers.CustomSerial.Tests/
│
├── tools/
│   └── S7Tester/                         # 独立 S7 连接测试工具 (WinExe)
│
└── document/                             # 设计文档
    ├── 通用上位机管理软件架构设计方案.md
    ├── NET8上位机代码架构与多屏设计方案.md
    ├── 异构下位机可配置SCADA界面架构设计方案.md
    ├── 系统待开发功能规划与路线图.md
    └── 写优先插队调度器实现逻辑.md
```

---

## 核心能力

### 1. 写优先插队调度

- 每个物理通道独立工作线程，互不阻塞
- 写指令通过 `Channel<WriteJob>` 无界异步队列投递，优先于周期读轮询执行
- 两道插队关卡：循环开头排空写队列 + 每个设备读取前再检查一次
- 详见 [写优先插队调度器实现逻辑.md](document/写优先插队调度器实现逻辑.md)

### 2. 实时数据总线

- 内存快照模型，读取不走数据库
- 死区过滤（Deadband）抑制无意义的微小变化通知
- 发布/订阅模式，UI 控件按需订阅点位

### 3. 多协议驱动

- **Modbus**：支持 RTU / ASCII / TCP 三种传输模式，CRC/LRC 校验，大小端可配置
- **Siemens S7**：支持 S7-200/300/400/1200/1500/200Smart，COTP 握手，PDU 编解码
- **自定义串口**：可配置帧头/帧尾/校验方式，适配非标准设备

### 4. 报警引擎

- 多级阈值判定（高限/高高限/低限/低低限）
- 报警确认与恢复机制
- 声光联动（可绑定 UI 控件）

### 5. 组态设计器

- 拖拽式画布布局工业控件
- 控件属性绑定点位地址
- 多画面/多屏幕投影

### 6. 工业控件库

| 控件 | 用途 |
|------|------|
| PumpControl | 泵运行/停止/故障状态 |
| ValveControl | 阀门开/关/故障 |
| PipeControl | 管道流体动画 |
| TankLevelControl | 罐体液位显示 |
| TrendChartControl | 实时趋势曲线 |
| CircularGaugeControl | 圆形仪表盘 |
| NumericCardControl | 数值卡片显示 |
| StatusLedControl | 状态指示灯 |
| IoMatrixControl | IO 矩阵总览 |
| AlarmBannerControl | 报警滚动条幅 |
| ControlButtonControl | 控制下发按钮 |
| DisplayBoxControl | 数据展示框 |
| TextLabelControl | 文本标签 |
| DeviceStatusControl | 设备综合状态 |
| PanelContainerControl | 容器面板 |

---

## 环境要求

- **操作系统**：Windows 10/11 (x64)
- **.NET SDK**：.NET 8.0 及以上（[下载地址](https://dotnet.microsoft.com/download/dotnet/8.0)）
- **IDE**（可选）：Visual Studio 2022 17.8+ 或 JetBrains Rider
- **PLC 侧前提**：
  - Siemens：CPU 需勾选"允许 PUT/GET 通信访问"
  - DB 块需关闭"优化的块访问"（标准访问模式）

---

## 快速开始

### 1. 克隆并编译

```shell
git clone <repo-url>
cd AUTO
dotnet build Mal.UniversalScada.sln -c Release
```

### 2. 启动组态设计器

```shell
dotnet run --project src/Mal.UniversalScada.Configurator.Wpf -c Release
```

在设计器中完成通道/设备/点位配置后，保存到 SQLite 数据库（默认 `scada_config.db`）。

### 3. 启动运行时监控

```shell
dotnet run --project src/Mal.UniversalScada.UI.Wpf -c Release
```

运行时程序自动加载数据库配置，连接 PLC，开始实时采集。

### 4. 运行测试

```shell
dotnet test Mal.UniversalScada.sln
```

---

## 配置说明

系统配置存储在 SQLite 数据库中，主要表结构：

| 表名 | 内容 |
|------|------|
| `Channels` | 通信通道（TCP/串口参数） |
| `Devices` | 下位机设备（协议类型、站地址、机架/插槽） |
| `Tags` | 数据点位（地址、数据类型、死区、采集周期） |
| `UiViews` | 组态画面定义（控件类型、位置、绑定点位） |

### 通道配置示例

| 字段 | 值 | 说明 |
|------|----|------|
| ChannelType | TcpClient | TCP 客户端 |
| Host | 192.168.0.1 | PLC 的 IP 地址 |
| Port | 102 | S7 协议默认端口 |

### 设备配置示例（Siemens S7-1200）

| 字段 | 值 |
|------|----|
| ProtocolType | SiemensS7 |
| CustomProtocolName | `Cpu=S71200;Rack=0;Slot=1` |
| StationAddress | 0 |
| DefaultPollIntervalMs | 1000 |

### 点位配置示例

| 字段 | 值 |
|------|----|
| Address | `DB1.DBX0.0` |
| DataType | Bool |
| Deadband | 0 |
| Description | 启动按钮 |

---

## 支持的协议与地址格式

### Siemens S7

| 数据区 | 地址格式 | 示例 | 说明 |
|--------|---------|------|------|
| DB 区（位） | `DB{n}.DBX{byte}.{bit}` | `DB1.DBX0.0` | DB1 第 0 字节第 0 位 |
| DB 区（字节） | `DB{n}.DBB{byte}` | `DB1.DBB0` | DB1 第 0 字节 |
| DB 区（字） | `DB{n}.DBW{byte}` | `DB1.DBW0` | DB1 第 0 字（2 字节） |
| DB 区（双字） | `DB{n}.DBD{byte}` | `DB1.DBD0` | DB1 第 0 双字（4 字节） |
| M 区（位） | `M{byte}.{bit}` | `M0.0` | 标志位存储区 |
| I 区（位） | `I{byte}.{bit}` | `I0.0` | 输入映像区 |
| Q 区（位） | `Q{byte}.{bit}` | `Q0.0` | 输出映像区 |

> 简写兼容：`DB1.0.0` 等价于 `DB1.DBX0.0`。

### Modbus

| 数据区 | 地址格式 | 示例 |
|--------|---------|------|
| 线圈 | `{addr}` | `100` |
| 离散输入 | `I{addr}` | `I100` |
| 输入寄存器 | `IR{addr}` | `IR100` |
| 保持寄存器 | `HR{addr}` | `HR100` |

> 支持配置字序（大端/小端/字交换）适配不同设备。

---

## S7Tester 工具

独立的 S7 协议连接测试工具，不依赖主项目，用于验证 PLC 连通性和点位读写。

### 启动

```shell
cd tools/S7Tester
dotnet run
```

### 使用

1. 输入 PLC 的 IP 地址、机架号（S7-1200 默认 0）、插槽号（S7-1200 默认 1）、CPU 类型
2. 点击"连接测试"验证通信
3. 在地址框中输入点位地址（每行一个，如 `DB1.DBX0.0`）
4. 点击"读取数据"获取点位值

### 自包含发布

目标机器无需安装 .NET 8 运行时：

```shell
dotnet publish tools/S7Tester/S7Tester.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

产物在 `tools/S7Tester/bin/Release/net8.0-windows/win-x64/publish/`，拷走即可运行。

---

## 设计文档

| 文档 | 内容 |
|------|------|
| [通用上位机管理软件架构设计方案.md](document/通用上位机管理软件架构设计方案.md) | 整体分层架构、模块划分 |
| [NET8上位机代码架构与多屏设计方案.md](document/NET8上位机代码架构与多屏设计方案.md) | .NET 8 技术选型、多屏投影 |
| [异构下位机可配置SCADA界面架构设计方案.md](document/异构下位机可配置SCADA界面架构设计方案.md) | 可配置界面、控件体系 |
| [系统待开发功能规划与路线图.md](document/系统待开发功能规划与路线图.md) | 功能规划与里程碑 |
| [写优先插队调度器实现逻辑.md](document/写优先插队调度器实现逻辑.md) | 调度引擎深度解析 |

---

## 常见问题

### Q: 程序启动后窗口不显示？

确认目标机器已安装 .NET 8 桌面运行时（`Microsoft.WindowsDesktop.App 8.0`）。WinExe 类型在缺少运行时时会静默退出。可使用自包含发布方式规避。

### Q: S7 连接报 "TPKT is incomplete / invalid"？

TCP 通了但协议握手失败，按优先级排查：

1. PLC 未勾选"允许 PUT/GET 通信访问"（TIA Portal → CPU 属性 → 保护与安全）
2. 使用的是 PLCSIM Basic（不支持外部 S7 连接），需用 PLCSIM Advanced
3. 机架/插槽号不匹配（S7-1200 固定 Rack=0, Slot=1）
4. PLC 连接资源已占满

### Q: DB 块读取地址偏移全为空？

TIA Portal 中 DB 块如果勾选了"优化的块访问"，将不显示偏移量。需在 DB 属性中取消该勾选，改为标准访问模式后重新下载。

### Q: 串口设备通信不稳定？

检查波特率/数据位/停止位/校验位是否与设备一致，自定义串口协议需确认帧头/帧尾/校验方式配置正确。

---

## 许可协议

本项目采用 [非商用开源许可协议](LICENSE)。使用前请阅读协议全文。

- 允许：个人学习、学术研究、教育用途
- 禁止：任何形式的商业用途
- 商用授权：请联系版权持有人
