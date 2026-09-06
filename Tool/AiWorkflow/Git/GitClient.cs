using System.Diagnostics;
using System.Text;
using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Git;

public sealed record GitCommandResult(int ExitCode, string Output, string Error);
public sealed record GitStatusEntry(char IndexStatus, char WorktreeStatus, string Path, string? OriginalPath = null);

public sealed class GitClient(string root)
{
    public GitCommandResult Run(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };
        process.StartInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        foreach (var arg in arguments) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000))
        {
            process.Kill(true); process.WaitForExit();
            throw new WorkflowException("Git 只读检查超时。", "git.timeout");
        }
        Task.WaitAll(stdout, stderr);
        return new(process.ExitCode, stdout.Result, stderr.Result);
    }

    public static IReadOnlyList<GitStatusEntry> ParseStatus(string text)
    {
        var records = text.Split('\0'); var entries = new List<GitStatusEntry>();
        for (var i = 0; i < records.Length; i++)
        {
            var record = records[i]; if (record.Length == 0) continue;
            if (record.Length < 4 || record[2] != ' ') throw new WorkflowException("Git 状态记录无效。", "git.status");
            string? original = null;
            if (record[0] is 'R' or 'C' || record[1] is 'R' or 'C')
            {
                if (++i >= records.Length || records[i].Length == 0) throw new WorkflowException("Git 重命名记录不完整。", "git.status");
                original = records[i].Replace('\\', '/');
            }
            entries.Add(new(record[0], record[1], record[3..].Replace('\\', '/'), original));
        }
        return entries;
    }
}
