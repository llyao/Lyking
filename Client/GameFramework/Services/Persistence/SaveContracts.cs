using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.Persistence
{
    /// <summary>按记录名读写临时存档，并以一次原子操作替换当前记录。</summary>
    public interface IAtomicSaveStore
    {
        ValueTask<byte[]?> ReadCurrentAsync(string key, CancellationToken cancellationToken);
        ValueTask WriteTemporaryAsync(string key, string operationId, byte[] data, CancellationToken cancellationToken);
        ValueTask<byte[]?> ReadTemporaryAsync(string key, string operationId, CancellationToken cancellationToken);
        ValueTask ActivateTemporaryAsync(string key, string operationId);
        ValueTask DiscardTemporaryAsync(string key, string operationId, CancellationToken cancellationToken);
    }

    /// <summary>将存档明文包装为平台保护格式；密钥保管由平台适配器负责。</summary>
    public interface ISaveDataProtector
    {
        byte[] Protect(string key, byte[] plainData);
        byte[] Unprotect(string key, byte[] protectedData);
    }

    /// <summary>纯逻辑测试与无保护模拟使用的原样保护器，不提供保密性。</summary>
    public sealed class IdentitySaveDataProtector : ISaveDataProtector
    {
        public byte[] Protect(string key, byte[] plainData) => (byte[])(plainData ?? throw new ArgumentNullException(nameof(plainData))).Clone();
        public byte[] Unprotect(string key, byte[] protectedData) => (byte[])(protectedData ?? throw new ArgumentNullException(nameof(protectedData))).Clone();
    }

    /// <summary>一次只负责相邻两个格式版本的迁移。</summary>
    public interface ISaveDataMigrator
    {
        int FromVersion { get; }
        int ToVersion { get; }
        ValueTask<byte[]> MigrateAsync(byte[] payload, CancellationToken cancellationToken);
    }

    public class SaveDataException : Exception
    {
        public SaveDataException(string message) : base(message) { }
        public SaveDataException(string message, Exception innerException) : base(message, innerException) { }
    }

    public sealed class SaveRecordNotFoundException : SaveDataException
    {
        public SaveRecordNotFoundException(string key) : base($"存档记录不存在：{key}。") { }
    }

    public sealed class SaveDataIntegrityException : SaveDataException
    {
        public SaveDataIntegrityException(string message) : base(message) { }
        public SaveDataIntegrityException(string message, Exception innerException) : base(message, innerException) { }
    }

    public sealed class UnsupportedSaveVersionException : SaveDataException
    {
        public UnsupportedSaveVersionException(int version, int supportedVersion)
            : base($"存档版本 {version} 高于当前支持版本 {supportedVersion}。")
        {
            Version = version;
            SupportedVersion = supportedVersion;
        }

        public int Version { get; }
        public int SupportedVersion { get; }
    }
}
