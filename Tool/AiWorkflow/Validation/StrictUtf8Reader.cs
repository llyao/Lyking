using System.Text;
using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Validation;

public static class StrictUtf8Reader
{
    public static string Read(string path)
    {
        try { return new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path)).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { throw new WorkflowException("文件包含无效 UTF-8 字节。", "encoding.invalid-utf8"); }
    }
}
