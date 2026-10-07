using System;
using System.Collections.Generic;

namespace Lyking.GameFramework.Client.Services.Localization
{
    /// <summary>
    /// 本地化抽象：多语言词条表 + 当前语言切换。
    /// 查找顺序：当前语言 → 回退语言 → 返回 key 本身并触发 MissingTranslation。
    /// </summary>
    public interface ILocalizationService
    {
        string CurrentLanguage { get; }

        /// <summary>切换语言；未知语言抛 ArgumentException（词条表未注册）。</summary>
        void SetLanguage(string language);

        /// <summary>取词条并按 string.Format 格式化。</summary>
        string Get(string key, params object[] args);

        /// <summary>注册或合并某语言词条表。</summary>
        void AddTable(string language, IReadOnlyDictionary<string, string> table);

        bool HasLanguage(string language);

        /// <summary>语言切换完成事件。</summary>
        event Action<string>? LanguageChanged;

        /// <summary>任何语言都缺失词条时触发（key, 语言）。</summary>
        event Action<string, string>? MissingTranslation;
    }

    /// <summary>ILocalizationService 默认实现。</summary>
    public sealed class LocalizationService : ILocalizationService
    {
        private readonly string _fallbackLanguage;
        private readonly Dictionary<string, Dictionary<string, string>> _tables =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        public LocalizationService(string defaultLanguage = "zh-Hans", string? fallbackLanguage = null)
        {
            if (string.IsNullOrWhiteSpace(defaultLanguage))
            {
                throw new ArgumentException("默认语言不能为空。", nameof(defaultLanguage));
            }

            CurrentLanguage = defaultLanguage;
            _fallbackLanguage = fallbackLanguage ?? defaultLanguage;
        }

        public string CurrentLanguage { get; private set; }

        public event Action<string>? LanguageChanged;

        public event Action<string, string>? MissingTranslation;

        public bool HasLanguage(string language) =>
            !string.IsNullOrWhiteSpace(language) && _tables.ContainsKey(language);

        public void SetLanguage(string language)
        {
            if (!_tables.ContainsKey(language ?? throw new ArgumentNullException(nameof(language))))
            {
                throw new ArgumentException($"语言 {language} 的词条表未注册。", nameof(language));
            }

            if (string.Equals(CurrentLanguage, language, StringComparison.Ordinal))
            {
                return;
            }

            CurrentLanguage = language;
            LanguageChanged?.Invoke(language);
        }

        public void AddTable(string language, IReadOnlyDictionary<string, string> table)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentException("语言不能为空。", nameof(language));
            }

            if (table is null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (!_tables.TryGetValue(language, out Dictionary<string, string>? merged))
            {
                merged = new Dictionary<string, string>(StringComparer.Ordinal);
                _tables[language] = merged;
            }

            foreach (KeyValuePair<string, string> entry in table)
            {
                merged[entry.Key] = entry.Value;
            }
        }

        public string Get(string key, params object[] args)
        {
            string value = Lookup(key) ?? ReportMissing(key);
            return args != null && args.Length > 0 ? string.Format(value, args) : value;
        }

        private string? Lookup(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("词条 key 不能为空。", nameof(key));
            }

            if (_tables.TryGetValue(CurrentLanguage, out Dictionary<string, string>? table)
                && table.TryGetValue(key, out string? value))
            {
                return value;
            }

            if (!string.Equals(_fallbackLanguage, CurrentLanguage, StringComparison.Ordinal)
                && _tables.TryGetValue(_fallbackLanguage, out Dictionary<string, string>? fallbackTable)
                && fallbackTable.TryGetValue(key, out string? fallbackValue))
            {
                return fallbackValue;
            }

            return null;
        }

        private string ReportMissing(string key)
        {
            MissingTranslation?.Invoke(key, CurrentLanguage);
            return key;
        }
    }
}
