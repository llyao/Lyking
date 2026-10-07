using System;
using System.Collections.Generic;

namespace Lyking.GameFramework.Client.Services.Config
{
    /// <summary>配置读取抽象：原始值为字符串，类型解析由调用方的 parser 完成（JSON/表格解析在 Unity 侧或工具链接入）。</summary>
    public interface IConfigProvider
    {
        bool TryGetRaw(string key, out string value);

        /// <summary>取原始值；缺失抛 KeyNotFoundException。</summary>
        string GetRaw(string key);

        /// <summary>取解析值；缺失抛 KeyNotFoundException，解析异常原样上抛。</summary>
        T Get<T>(string key, Func<string, T> parser);

        bool TryGet<T>(string key, Func<string, T> parser, out T value);
    }

    /// <summary>内存配置存储：启动时注入，运行期只读为主（允许热更新 SetRaw）。</summary>
    public sealed class ConfigStore : IConfigProvider
    {
        private readonly Dictionary<string, string> _values;

        public ConfigStore(IReadOnlyDictionary<string, string>? initialValues = null)
        {
            _values = initialValues != null
                ? new Dictionary<string, string>(initialValues)
                : new Dictionary<string, string>();
        }

        public int Count => _values.Count;

        public bool TryGetRaw(string key, out string value) => _values.TryGetValue(key ?? throw new ArgumentNullException(nameof(key)), out value!);

        public string GetRaw(string key)
        {
            if (TryGetRaw(key, out string? value))
            {
                return value;
            }

            throw new System.Collections.Generic.KeyNotFoundException($"配置 {key} 不存在。");
        }

        public T Get<T>(string key, Func<string, T> parser)
        {
            if (parser is null)
            {
                throw new ArgumentNullException(nameof(parser));
            }

            return parser(GetRaw(key));
        }

        public bool TryGet<T>(string key, Func<string, T> parser, out T value)
        {
            if (parser is null)
            {
                throw new ArgumentNullException(nameof(parser));
            }

            if (TryGetRaw(key, out string? raw))
            {
                value = parser(raw);
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>写入或覆盖配置（热更场景）。</summary>
        public void SetRaw(string key, string value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _values[key] = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}
