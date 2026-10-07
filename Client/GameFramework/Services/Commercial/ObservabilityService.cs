using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Lyking.GameFramework.Client.Services.Commercial
{
    public enum DiagnosticKind
    {
        Analytics,
        Crash,
        Performance,
    }

    public sealed class DiagnosticRecord
    {
        internal DiagnosticRecord(DiagnosticKind kind, string name, IReadOnlyDictionary<string, object?> fields)
        {
            Kind = kind;
            Name = name;
            Fields = fields;
        }

        public DiagnosticKind Kind { get; }
        public string Name { get; }
        public IReadOnlyDictionary<string, object?> Fields { get; }
    }

    public interface IObservabilityService
    {
        int PendingCount { get; }
        long DroppedCount { get; }
        bool Track(string name, IReadOnlyDictionary<string, object?>? fields = null);
        bool RecordCrash(string exceptionType, string? message = null);
        bool RecordPerformance(string metric, double value);
        IReadOnlyList<DiagnosticRecord> Drain(int maximumCount = int.MaxValue);
    }

    /// <summary>仅用于模拟与本地测试；不执行磁盘或网络 I/O，达到容量时丢弃最新记录。</summary>
    public sealed class SimulationObservabilityService : IObservabilityService, IDisposable
    {
        private const int MaximumDiagnosticFields = 32;
        private const int MaximumInspectedFields = 64;
        private const int MaximumFieldKeyLength = 64;
        private const int MaximumInputStringLength = 1024;
        private static readonly string[] SensitiveKeyFragments =
        {
            "email", "phone", "mobile", "userid", "account", "token", "password", "receipt",
            "playerid", "customerid", "sessionid", "socialid", "deviceid", "advertisingid",
            "ipaddress", "address", "location", "latitude", "longitude", "birthdate", "realname",
            "username", "openid", "credential", "identifier", "uuid", "guid", "hash", "serial",
        };

        private readonly object _gate = new object();
        private readonly Queue<DiagnosticRecord> _queue = new Queue<DiagnosticRecord>();
        private readonly IConsentService _consent;
        private readonly int _capacity;
        private long _droppedCount;
        private bool _disposed;

        public SimulationObservabilityService(IConsentService consent, int capacity)
        {
            _consent = consent ?? throw new ArgumentNullException(nameof(consent));
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
            _consent.Changed += OnConsentChanged;
        }

        public int PendingCount
        {
            get { lock (_gate) { return _queue.Count; } }
        }

        public long DroppedCount
        {
            get { lock (_gate) { return _droppedCount; } }
        }

        public bool Track(string name, IReadOnlyDictionary<string, object?>? fields = null) =>
            Enqueue(DiagnosticKind.Analytics, name, ConsentPurpose.Analytics, fields);

        public bool RecordCrash(string exceptionType, string? message = null)
        {
            string safeType = SanitizeName(exceptionType, nameof(exceptionType));
            _ = message;
            return Enqueue(DiagnosticKind.Crash, safeType, ConsentPurpose.CrashReporting,
                null);
        }

        public bool RecordPerformance(string metric, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            string safeMetric = SanitizeName(metric, nameof(metric));
            var fields = new Dictionary<string, object?> { ["value"] = value };
            return Enqueue(DiagnosticKind.Performance, safeMetric, ConsentPurpose.Performance, fields);
        }

        public IReadOnlyList<DiagnosticRecord> Drain(int maximumCount = int.MaxValue)
        {
            if (maximumCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCount));
            }

            lock (_gate)
            {
                var records = new List<DiagnosticRecord>(Math.Min(maximumCount, _queue.Count));
                while (records.Count < maximumCount && _queue.Count > 0)
                {
                    records.Add(_queue.Dequeue());
                }

                return records;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _queue.Clear();
            }

            _consent.Changed -= OnConsentChanged;
        }

        private bool Enqueue(
            DiagnosticKind kind,
            string name,
            ConsentPurpose purpose,
            IReadOnlyDictionary<string, object?>? fields)
        {
            string safeName = SanitizeName(name, nameof(name));
            lock (_gate)
            {
                if (_disposed || !_consent.HasConsent(purpose))
                {
                    return false;
                }

                if (_queue.Count >= _capacity)
                {
                    _droppedCount++;
                    return false;
                }

                _queue.Enqueue(new DiagnosticRecord(kind, safeName, SanitizeFields(fields)));
                return true;
            }
        }

        private void OnConsentChanged(ConsentChanged change)
        {
            if (change.Granted || (change.Purpose != ConsentPurpose.Analytics
                && change.Purpose != ConsentPurpose.CrashReporting
                && change.Purpose != ConsentPurpose.Performance))
            {
                return;
            }

            lock (_gate)
            {
                if (_queue.Count == 0)
                {
                    return;
                }

                var retained = _queue.Where(record => !IsForPurpose(record, change.Purpose)).ToArray();
                _queue.Clear();
                foreach (DiagnosticRecord record in retained)
                {
                    _queue.Enqueue(record);
                }
            }
        }

        private static bool IsForPurpose(DiagnosticRecord record, ConsentPurpose purpose) => purpose switch
        {
            ConsentPurpose.Analytics => record.Kind == DiagnosticKind.Analytics,
            ConsentPurpose.CrashReporting => record.Kind == DiagnosticKind.Crash,
            ConsentPurpose.Performance => record.Kind == DiagnosticKind.Performance,
            _ => false,
        };

        private static IReadOnlyDictionary<string, object?> SanitizeFields(IReadOnlyDictionary<string, object?>? fields)
        {
            var safe = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (fields != null)
            {
                int inspected = 0;
                foreach (KeyValuePair<string, object?> field in fields)
                {
                    if (inspected++ >= MaximumInspectedFields || safe.Count >= MaximumDiagnosticFields)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(field.Key)
                        || field.Key.Length > MaximumFieldKeyLength
                        || IsSensitiveKey(field.Key)
                        || field.Value == null)
                    {
                        continue;
                    }

                    object? value = SanitizeValue(field.Value);
                    if (value != null)
                    {
                        safe[field.Key] = value;
                    }
                }
            }

            return new ReadOnlyDictionary<string, object?>(safe);
        }

        private static object? SanitizeValue(object value)
        {
            if (value is string text)
            {
                string bounded = text.Length > MaximumInputStringLength
                    ? text.Substring(0, MaximumInputStringLength)
                    : text;
                string sanitized = new string(bounded.Where(character => !char.IsControl(character)).Take(256).ToArray());
                return sanitized.Contains("@", StringComparison.Ordinal) || LooksLikePhoneNumber(sanitized)
                    ? null
                    : sanitized;
            }

            Type type = value.GetType();
            return type.IsEnum || type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte)
                || type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint)
                || type == typeof(long) || type == typeof(ulong) || type == typeof(float) || type == typeof(double)
                || type == typeof(decimal) ? value : null;
        }

        private static bool IsSensitiveKey(string key)
        {
            string normalized = new string(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            bool identifierSuffix = key.Equals("id", StringComparison.OrdinalIgnoreCase)
                || key.EndsWith("Id", StringComparison.Ordinal)
                || key.EndsWith("ID", StringComparison.Ordinal)
                || key.EndsWith("_id", StringComparison.OrdinalIgnoreCase)
                || key.EndsWith("-id", StringComparison.OrdinalIgnoreCase)
                || key.EndsWith(".id", StringComparison.OrdinalIgnoreCase);
            return identifierSuffix || SensitiveKeyFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal));
        }

        private static bool LooksLikePhoneNumber(string value)
        {
            int digits = 0;
            foreach (char character in value)
            {
                if (char.IsDigit(character))
                {
                    digits++;
                }
                else if (character != '+' && character != '-' && character != '(' && character != ')'
                    && character != ' ' && character != '.')
                {
                    return false;
                }
            }

            return digits is >= 7 and <= 15;
        }

        private static string SanitizeName(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.Length > 96
                || value.Any(character => !(char.IsLetterOrDigit(character) || character == '.' || character == '_' || character == '-')))
            {
                throw new ArgumentException("名称只能包含字母、数字、点、下划线和连字符。", parameterName);
            }

            return value;
        }
    }
}
