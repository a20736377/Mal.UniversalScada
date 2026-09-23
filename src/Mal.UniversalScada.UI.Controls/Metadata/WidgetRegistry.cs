using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mal.UniversalScada.Core.Models;
using Mal.UniversalScada.UI.Controls.ViewModels;

namespace Mal.UniversalScada.UI.Controls.Metadata;

/// <summary>
/// SCADA 工业组件统一反射注册中心。
/// 支持多组件程序集（当前程序集、其他已加载程序集、运行时动态载入程序集以及外部插件目录 DLL），
/// 自动扫描提取 [ScadaWidget] 与 IScadaWidgetPackage，提供多态工厂与元数据查询。
/// </summary>
public class WidgetRegistry
{
    private static readonly Lazy<WidgetRegistry> _lazy = new(() => new WidgetRegistry());
    public static WidgetRegistry Instance => _lazy.Value;

    private readonly ConcurrentDictionary<string, WidgetDescriptor> _descriptorsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<WidgetType, WidgetDescriptor> _descriptorsByEnum = new();
    private readonly ConcurrentDictionary<Type, WidgetDescriptor> _descriptorsByVmType = new();
    private readonly HashSet<string> _scannedAssemblyNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    /// <summary>
    /// 当有新组件类库或组件注册发生变化时触发通知
    /// </summary>
    public event EventHandler? RegistryChanged;

    public WidgetRegistry()
    {
        // 1. 首先注册当前类库 Mal.UniversalScada.UI.Controls
        RegisterAssembly(typeof(WidgetRegistry).Assembly);

        // 2. 扫描当前 AppDomain 中已载入的所有程序集
        try
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in loadedAssemblies)
            {
                if (!asm.IsDynamic)
                {
                    RegisterAssembly(asm);
                }
            }
        }
        catch
        {
            // 忽略非关键程序集读取异常
        }

        // 3. 监听运行时后续动态载入的程序集
        AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>
        {
            if (!args.LoadedAssembly.IsDynamic)
            {
                RegisterAssembly(args.LoadedAssembly);
            }
        };
    }

    /// <summary>
    /// 注册指定程序集（扫描其中的 [ScadaWidget] 组件及 IScadaWidgetPackage 扩展包）
    /// </summary>
    public void RegisterAssembly(Assembly assembly)
    {
        if (assembly == null) return;

        var asmName = assembly.GetName().Name ?? string.Empty;
        lock (_syncLock)
        {
            if (_scannedAssemblyNames.Contains(asmName))
                return;
            _scannedAssemblyNames.Add(asmName);
        }

        bool hasChanges = false;

        try
        {
            // 1. 扫描是否有 IScadaWidgetPackage 扩展包入口并调用初始化
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).Select(t => t!).ToArray();
            }

            foreach (var type in types)
            {
                if (typeof(IScadaWidgetPackage).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IScadaWidgetPackage package)
                        {
                            package.Initialize(this);
                            hasChanges = true;
                        }
                    }
                    catch
                    {
                        // 忽略单个扩展包实例创建异常
                    }
                }
            }

            // 2. 扫描所有标记了 [ScadaWidget] 的 ViewModel 类型
            foreach (var type in types)
            {
                if (!type.IsAbstract && typeof(WidgetViewModel).IsAssignableFrom(type))
                {
                    var attr = type.GetCustomAttribute<ScadaWidgetAttribute>();
                    if (attr != null)
                    {
                        var descriptor = new WidgetDescriptor
                        {
                            TypeId = attr.TypeId,
                            Type = attr.Type,
                            DisplayName = attr.DisplayName,
                            Icon = attr.Icon,
                            Category = attr.Category,
                            Description = attr.Description,
                            Order = attr.Order,
                            DefaultWidth = attr.DefaultWidth,
                            DefaultHeight = attr.DefaultHeight,
                            DefaultTitle = attr.DefaultTitle,
                            ViewModelType = type,
                            ViewType = attr.ViewType,
                            PropertyEditorType = attr.PropertyEditorType,
                            Assembly = assembly
                        };

                        RegisterWidget(descriptor, false);
                        hasChanges = true;
                    }
                }
            }
        }
        catch
        {
            // 忽略某些系统/第三方程序集反射扫描的异常
        }

        if (hasChanges)
        {
            RegistryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 从外部插件目录扫描载入所有的 .dll 组件类库
    /// </summary>
    /// <param name="directoryPath">外部组件 DLL 所在目录</param>
    public int ScanDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            return 0;

        int loadedCount = 0;
        var dllFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                var asm = Assembly.LoadFrom(dllPath);
                RegisterAssembly(asm);
                loadedCount++;
            }
            catch
            {
                // 忽略非 .NET 或无法加载的动态库
            }
        }

        return loadedCount;
    }

    /// <summary>
    /// 手动注册单个组件描述器
    /// </summary>
    public void RegisterWidget(WidgetDescriptor descriptor, bool notify = true)
    {
        if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.TypeId))
            return;

        _descriptorsById[descriptor.TypeId] = descriptor;

        if (descriptor.Type != WidgetType.Custom)
        {
            _descriptorsByEnum[descriptor.Type] = descriptor;
        }

        if (descriptor.ViewModelType != null)
        {
            _descriptorsByVmType[descriptor.ViewModelType] = descriptor;
        }

        if (notify)
        {
            RegistryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 手动注册扩展组件包
    /// </summary>
    public void RegisterPackage(IScadaWidgetPackage package)
    {
        if (package == null) return;
        package.Initialize(this);
        RegistryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 获取所有已注册组件描述器（按分类与排序权重排序）
    /// </summary>
    public IReadOnlyList<WidgetDescriptor> GetAllWidgets()
    {
        return _descriptorsById.Values
            .OrderBy(w => w.Category)
            .ThenBy(w => w.Order)
            .ToList();
    }

    /// <summary>
    /// 获取当前所有已注册的组件分类名称
    /// </summary>
    public IReadOnlyList<string> GetCategories()
    {
        return _descriptorsById.Values
            .Select(w => w.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
    }

    /// <summary>
    /// 根据全局唯一 TypeId 获取组件描述器
    /// </summary>
    public WidgetDescriptor? GetDescriptor(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return null;
        return _descriptorsById.TryGetValue(typeId, out var desc) ? desc : null;
    }

    /// <summary>
    /// 根据标准枚举类型获取组件描述器
    /// </summary>
    public WidgetDescriptor? GetDescriptor(WidgetType type)
    {
        return _descriptorsByEnum.TryGetValue(type, out var desc) ? desc : null;
    }

    /// <summary>
    /// 根据 ViewModel 类型获取组件描述器
    /// </summary>
    public WidgetDescriptor? GetDescriptor(Type viewModelType)
    {
        if (viewModelType == null) return null;
        if (_descriptorsByVmType.TryGetValue(viewModelType, out var desc))
            return desc;

        // 若直接匹配不到，尝试匹配派生类
        return _descriptorsByVmType.FirstOrDefault(kv => kv.Key.IsAssignableFrom(viewModelType)).Value;
    }

    /// <summary>
    /// 根据 TypeId 创建组件 ViewModel (别名)
    /// </summary>
    public WidgetViewModel CreateViewModel(string typeId, bool isDesignMode = false) => Create(typeId, isDesignMode);

    /// <summary>
    /// 根据 WidgetType 枚举创建组件 ViewModel (别名)
    /// </summary>
    public WidgetViewModel CreateViewModel(WidgetType type, bool isDesignMode = false) => Create(type, isDesignMode);

    /// <summary>
    /// 根据 TypeId 创建组件 ViewModel
    /// </summary>
    public WidgetViewModel Create(string typeId, bool isDesignMode = false)
    {
        var desc = GetDescriptor(typeId);
        if (desc != null)
        {
            return desc.CreateViewModel(isDesignMode);
        }

        // 若未找到且能解析为枚举，则按枚举尝试
        if (Enum.TryParse<WidgetType>(typeId, true, out var parsedEnum))
        {
            return Create(parsedEnum, isDesignMode);
        }

        // 兜底回退：创建基础数显卡片
        var fallbackDesc = GetDescriptor(WidgetType.NumericCard);
        if (fallbackDesc != null)
        {
            return fallbackDesc.CreateViewModel(isDesignMode);
        }

        return new NumericCardWidgetViewModel { IsDesignMode = isDesignMode };
    }

    /// <summary>
    /// 根据 WidgetType 枚举创建组件 ViewModel
    /// </summary>
    public WidgetViewModel Create(WidgetType type, bool isDesignMode = false)
    {
        var desc = GetDescriptor(type);
        if (desc != null)
        {
            return desc.CreateViewModel(isDesignMode);
        }

        // 兜底创建
        return new NumericCardWidgetViewModel
        {
            Type = type,
            IsDesignMode = isDesignMode,
            Width = 180,
            Height = 140
        };
    }

    /// <summary>
    /// 从 WidgetConfig 元数据无损反序列化创建组件 ViewModel
    /// </summary>
    public WidgetViewModel FromConfig(WidgetConfig config, bool isDesignMode = false)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        WidgetViewModel vm;

        // 如果是自定义类型或者指定了 CustomTypeName
        if (!string.IsNullOrWhiteSpace(config.CustomTypeName))
        {
            vm = Create(config.CustomTypeName, isDesignMode);
        }
        else
        {
            vm = Create(config.Type, isDesignMode);
        }

        vm.Id = config.WidgetId;
        vm.GroupId = config.GroupId;
        vm.Title = config.Title;
        vm.PrimaryTagId = config.PrimaryTagId;
        vm.X = config.X;
        vm.Y = config.Y;

        var desc = GetDescriptor(vm.GetType()) ?? GetDescriptor(config.Type);
        vm.Width = config.Width > 0 ? config.Width : (desc?.DefaultWidth ?? 180);
        vm.Height = config.Height > 0 ? config.Height : (desc?.DefaultHeight ?? 140);

        foreach (var kvp in config.Properties)
        {
            vm.Properties[kvp.Key] = kvp.Value;
        }

        vm.LoadProperties(vm.Properties);
        vm.UpdateRuntimeValue(0);

        return vm;
    }
}
