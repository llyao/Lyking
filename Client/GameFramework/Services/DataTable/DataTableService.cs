using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Lyking.GameFramework.Client.Core.Modules;

namespace Lyking.GameFramework.Client.Services.DataTable
{
    /// <summary>
    /// 数据行合同：表格驱动的静态配置（角色、道具、关卡参数）。
    /// 行类型自带解析：从单元格字符串数组填充字段，解析失败返回 false。
    /// </summary>
    public interface IDataRow
    {
        /// <summary>行主键。</summary>
        int Id { get; }

        /// <summary>从单元格填充本行；格式非法返回 false。</summary>
        bool ParseRow(IReadOnlyList<string> cells);
    }

    /// <summary>表格解析异常：携带行号与原因。</summary>
    public sealed class DataTableParseException : Exception
    {
        internal DataTableParseException(int lineIndex, string reason)
            : base($"数据表第 {lineIndex} 行解析失败：{reason}")
        {
            LineIndex = lineIndex;
        }

        /// <summary>文本中的行号（0 基，含表头与空行计数）。</summary>
        public int LineIndex { get; }
    }

    /// <summary>加载后的只读数据表。</summary>
    public sealed class DataTable<TTable> where TTable : class, IDataRow
    {
        private readonly Dictionary<int, TTable> _rows;

        internal DataTable(string name, Dictionary<int, TTable> rows, int skippedRowCount)
        {
            Name = name;
            _rows = rows;
            SkippedRowCount = skippedRowCount;
        }

        public string Name { get; }

        public int Count => _rows.Count;

        /// <summary>本次加载因解析失败被跳过的行数（strict=false 时产生）。</summary>
        public int SkippedRowCount { get; }

        public bool HasRow(int id) => _rows.ContainsKey(id);

        public bool TryGetRow(int id, out TTable row) => _rows.TryGetValue(id, out row!);

        /// <summary>取行；不存在抛 KeyNotFoundException。</summary>
        public TTable GetRow(int id)
        {
            if (_rows.TryGetValue(id, out TTable? row))
            {
                return row;
            }

            throw new KeyNotFoundException($"数据表 {Name} 不存在 id={id} 的行。");
        }

        public IReadOnlyCollection<TTable> Rows => _rows.Values;

        public IEnumerable<TTable> Where(Func<TTable, bool> predicate) => _rows.Values.Where(predicate);
    }

    /// <summary>
    /// 数据表服务：从文本加载表格（制表符/CSV 均可，由 delimiter 决定），
    /// 集中注册并按表名检索。表格文本来源由资源层决定（Unity 侧可接 Addressables/AssetBundle）。
    /// 重复加载同名表抛 InvalidOperationException，需要覆盖时先 RemoveTable。
    /// </summary>
    public sealed class DataTableService : ClientModule
    {
        private readonly Dictionary<string, object> _tables = new Dictionary<string, object>(StringComparer.Ordinal);

        public DataTableService() : base("DataTable", priority: 150)
        {
        }

        public int TableCount => _tables.Count;

        public bool HasTable(string name) => _tables.ContainsKey(name);

        /// <summary>
        /// 从文本加载数据表。strict=true 时任何解析失败/重复 id 抛 DataTableParseException；
        /// strict=false 时跳过坏行并计入表的 SkippedRowCount。
        /// </summary>
        public DataTable<TTable> LoadFromText<TTable>(
            string name,
            string text,
            char delimiter = '\t',
            bool skipFirstLine = true,
            bool strict = true) where TTable : class, IDataRow, new()
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("表名不能为空。", nameof(name));
            }

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (_tables.ContainsKey(name))
            {
                throw new InvalidOperationException($"数据表 {name} 已加载，覆盖前请先 RemoveTable。");
            }

            var rows = new Dictionary<int, TTable>();
            int skipped = 0;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (skipFirstLine && lineIndex == 0)
                {
                    continue;
                }

                string[] cells = line.Split(delimiter);
                var row = new TTable();
                if (!row.ParseRow(cells))
                {
                    if (strict)
                    {
                        throw new DataTableParseException(lineIndex, $"无法解析行内容：{line}");
                    }

                    skipped++;
                    continue;
                }

                if (!rows.TryAdd(row.Id, row))
                {
                    if (strict)
                    {
                        throw new DataTableParseException(lineIndex, $"重复的行 id={row.Id}。");
                    }

                    skipped++;
                    continue;
                }
            }

            var table = new DataTable<TTable>(name, rows, skipped);
            _tables[name] = table;
            return table;
        }

        public bool TryGetTable<TTable>(string name, [NotNullWhen(true)] out DataTable<TTable>? table) where TTable : class, IDataRow
        {
            if (_tables.TryGetValue(name ?? throw new ArgumentNullException(nameof(name)), out object? found)
                && found is DataTable<TTable> typed)
            {
                table = typed;
                return true;
            }

            table = null;
            return false;
        }

        /// <summary>取表；不存在或类型不符抛 InvalidOperationException。</summary>
        public DataTable<TTable> GetTable<TTable>(string name) where TTable : class, IDataRow
        {
            if (TryGetTable(name, out DataTable<TTable>? table))
            {
                return table;
            }

            throw new InvalidOperationException($"数据表 {name} 不存在或类型不是 {typeof(TTable).Name}。");
        }

        public bool RemoveTable(string name) => _tables.Remove(name);

        public void Clear() => _tables.Clear();
    }
}
