using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.Settings
{
    /// <summary>
    /// 本地设置存储抽象：键值形态的玩家偏好（音量、画质、语言等）。
    /// 注意：本接口不是存档系统——存档协议、版本迁移与完整性校验属于受控范围，另行立项。
    /// 真实持久化（PlayerPrefs/文件）由 Unity 适配层实现。
    /// </summary>
    public interface ISettingsStore
    {
        bool TryGet<T>(string key, out T value);

        /// <summary>写设置并触发 Changed；写入 null 抛 ArgumentNullException。</summary>
        void Set<T>(string key, T value);

        /// <summary>移除设置，返回是否存在。</summary>
        bool Remove(string key);

        /// <summary>持久化（内存实现为空操作）。</summary>
        ValueTask SaveAsync(CancellationToken cancellationToken = default);

        /// <summary>任一设置变更事件：(key)。</summary>
        event Action<string>? Changed;
    }

    /// <summary>ISettingsStore 内存实现：SaveAsync 只累计计数，供测试与无头环境。</summary>
    public sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        /// <summary>SaveAsync 调用次数（测试断言持久化时机）。</summary>
        public int SaveCount { get; private set; }

        public event Action<string>? Changed;

        public bool TryGet<T>(string key, out T value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (_values.TryGetValue(key, out object? found) && found is T typed)
            {
                value = typed;
                return true;
            }

            value = default!;
            return false;
        }

        public void Set<T>(string key, T value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value is null)
            {
                throw new ArgumentNullException(nameof(value), "设置值不能为 null，请用 Remove。");
            }

            _values[key] = value;
            Changed?.Invoke(key);
        }

        public bool Remove(string key)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return _values.Remove(key);
        }

        public ValueTask SaveAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCount++;
            return default;
        }
    }
}
