using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.Download
{
    /// <summary>
    /// 模拟下载代理：不产生真实 IO。总长由 lengthResolver 提供（缺省 1024），
    /// 分 chunks 段上报进度；failures 按 url 注入异常以测试失败/重试路径。
    /// 真实下载由 Unity 适配层（UnityWebRequest）或服务端工具（HttpClient）实现。
    /// </summary>
    public sealed class SimulationDownloadAgent : IDownloadAgent
    {
        private readonly Func<string, long>? _lengthResolver;
        private readonly int _chunks;
        private readonly Dictionary<string, Exception> _failures;

        public SimulationDownloadAgent(
            Func<string, long>? lengthResolver = null,
            int chunks = 8,
            IReadOnlyDictionary<string, Exception>? failures = null)
        {
            if (chunks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunks), "分段数必须为正。");
            }

            _lengthResolver = lengthResolver;
            _chunks = chunks;
            _failures = failures != null
                ? new Dictionary<string, Exception>(failures)
                : new Dictionary<string, Exception>();
        }

        public async ValueTask DownloadAsync(DownloadTask task, Action<double> onProgress, CancellationToken cancellationToken)
        {
            long total = _lengthResolver?.Invoke(task.Url) ?? 1024;
            if (total < 0)
            {
                throw new InvalidOperationException("总长不能为负。");
            }

            task.TotalLength = total;
            if (_failures.TryGetValue(task.Url, out Exception? failure))
            {
                throw failure;
            }

            for (int i = 1; i <= _chunks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                task.DownloadedLength = total * i / _chunks;
                onProgress(total == 0 ? 1d : (double)i / _chunks);
                await Task.Yield();
            }
        }
    }
}
