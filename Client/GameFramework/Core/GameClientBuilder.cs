using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.ServiceLocation;

namespace Lyking.GameFramework.Client
{
    /// <summary>
    /// GameClient 流式装配器：商业项目的推荐入口。
    /// <code>
    /// var client = GameClientBuilder.Create()
    ///     .WithSink(new UnityLogSink())
    ///     .WithModule&lt;TimerService&gt;()
    ///     .WithModulesFrom(typeof(ShopModule).Assembly)   // 按 [GameModule] 特性批量注册
    ///     .WithModule(new NetworkService(transport))
    ///     .ConfigureServices(services =&gt; services.Register&lt;IAssetProvider&gt;(provider))
    ///     .Build();
    /// await client.StartAsync();
    /// </code>
    /// Build 时即执行服务注册，模块在 StartAsync 才初始化；
    /// 重复模块名在 Build 阶段立即报错，不必等到启动。
    /// </summary>
    public sealed class GameClientBuilder
    {
        private readonly List<IClientModule> _modules = new List<IClientModule>();
        private readonly List<string> _moduleNames = new List<string>();
        private readonly List<Action<IServiceRegistry>> _serviceConfigurations = new List<Action<IServiceRegistry>>();
        private ILogger? _logger;
        private ILogSink? _sink;
        private LogLevel _minLevel = LogLevel.Info;

        private GameClientBuilder()
        {
        }

        public static GameClientBuilder Create() => new GameClientBuilder();

        /// <summary>直接指定日志器（优先于 WithSink）。</summary>
        public GameClientBuilder WithLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            return this;
        }

        /// <summary>指定日志输出端与阈值，由框架构造日志器。</summary>
        public GameClientBuilder WithSink(ILogSink sink, LogLevel minLevel = LogLevel.Info)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _minLevel = minLevel;
            return this;
        }

        /// <summary>注册模块（自动应用 [GameModule] 特性元数据）。</summary>
        public GameClientBuilder WithModule<TModule>(TModule module) where TModule : class, IClientModule
        {
            if (module is null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            AddModuleChecked(ModuleMetadata.ApplyAttribute(module, typeof(TModule)));
            return this;
        }

        /// <summary>注册模块（要求无参构造，通常配合 [GameModule] 特性）。</summary>
        public GameClientBuilder WithModule<TModule>() where TModule : class, IClientModule, new()
            => WithModule(new TModule());

        /// <summary>
        /// 扫描程序集，注册其中标注了 [GameModule] 的 IClientModule 实现（非抽象、可无参构造）。
        /// 未标注特性的模块类型会被忽略，避免把测试桩或基础设施类型误注册。
        /// IL2CPP 裁剪环境需要为被扫描程序集配置 link.xml 保留类型。
        /// </summary>
        public GameClientBuilder WithModulesFrom(Assembly assembly)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            foreach (Type type in assembly.GetTypes()
                         .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IClientModule).IsAssignableFrom(t))
                         .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                var attribute = (GameModuleAttribute?)Attribute.GetCustomAttribute(type, typeof(GameModuleAttribute));
                if (attribute == null)
                {
                    continue;
                }

                var module = (IClientModule?)Activator.CreateInstance(type);
                if (module == null)
                {
                    continue;
                }

                AddModuleChecked(new ModuleMetadataAdapter(
                    module,
                    attribute.Name ?? type.Name,
                    attribute.Priority,
                    attribute.Dependencies ?? Array.Empty<string>()));
            }

            return this;
        }

        /// <summary>注册服务配置回调，Build 时按注册顺序执行。</summary>
        public GameClientBuilder ConfigureServices(Action<IServiceRegistry> configure)
        {
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            _serviceConfigurations.Add(configure);
            return this;
        }

        /// <summary>装配客户端：建日志器、登记模块、执行服务配置。同名模块在此提前报错。</summary>
        public GameClient Build()
        {
            ILogger logger = _logger ?? new Logger(
                "GameClient",
                _sink ?? new DelegateLogSink((level, message) => Console.WriteLine($"[{level}] {message}")),
                _minLevel);

            var client = new GameClient(logger, _modules);
            foreach (Action<IServiceRegistry> configure in _serviceConfigurations)
            {
                configure(client.Services);
            }

            return client;
        }

        private void AddModuleChecked(IClientModule module)
        {
            if (_moduleNames.Contains(module.Name))
            {
                throw new ArgumentException($"模块名 {module.Name} 重复，无法装配。", nameof(module));
            }

            _moduleNames.Add(module.Name);
            _modules.Add(module);
        }
    }
}
