using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Lyking.GameFramework.Client.Services.ContentUpdate
{
    /// <summary>清单签名字节规范：固定域标签、版本、UTF-8 长度前缀、整数大端序、排序后的文件和轮换表。</summary>
    public static class ContentManifestCodec
    {
        private static readonly byte[] Domain = Encoding.ASCII.GetBytes("LYKING-CONTENT-MANIFEST\0");
        private static readonly UTF8Encoding Utf8 = new(false, true);

        public static byte[] EncodeCanonical(ContentManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            Validate(manifest);
            using var stream = new MemoryStream();
            stream.Write(Domain, 0, Domain.Length);
            WriteInt32(stream, 1);
            WriteString(stream, manifest.ApplicationId);
            WriteString(stream, manifest.PlatformId);
            WriteString(stream, manifest.Version);
            WriteInt64(stream, manifest.Sequence);
            WriteInt64(stream, manifest.NotBeforeUtc.UtcDateTime.Ticks);
            WriteInt64(stream, manifest.ExpiresAtUtc.UtcDateTime.Ticks);
            WriteString(stream, manifest.SigningKeyId);
            stream.WriteByte(manifest.IsRecovery ? (byte)1 : (byte)0);

            ContentFileEntry[] files = manifest.Files.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
            WriteInt32(stream, files.Length);
            foreach (ContentFileEntry file in files)
            {
                WriteString(stream, file.Path);
                WriteString(stream, file.Sha256);
                WriteInt64(stream, file.Length);
            }

            ContentKeyRotation[] rotations = manifest.Rotations.OrderBy(rotation => rotation.KeyId, StringComparer.Ordinal).ToArray();
            WriteInt32(stream, rotations.Length);
            foreach (ContentKeyRotation rotation in rotations)
            {
                WriteString(stream, rotation.KeyId);
                WriteBytes(stream, rotation.SubjectPublicKeyInfo);
                WriteInt64(stream, rotation.ActivateAtSequence);
                WriteInt64(stream, rotation.RetireAtSequence);
                WriteString(stream, rotation.RetireKeyId ?? string.Empty);
                WriteInt64(stream, rotation.RetireExistingKeyAtSequence ?? -1);
            }
            return stream.ToArray();
        }

        private static void Validate(ContentManifest manifest)
        {
            ValidateIdentifier(manifest.ApplicationId, nameof(manifest.ApplicationId));
            ValidateIdentifier(manifest.PlatformId, nameof(manifest.PlatformId));
            ValidateIdentifier(manifest.Version, nameof(manifest.Version));
            ValidateIdentifier(manifest.SigningKeyId, nameof(manifest.SigningKeyId));
            if (manifest.Sequence <= 0 || manifest.NotBeforeUtc >= manifest.ExpiresAtUtc)
                throw new ContentManifestFormatException("清单序号或有效时间范围无效。");
            if (manifest.Files.Count > 100000 || manifest.Rotations.Count > 64)
                throw new ContentManifestFormatException("清单条目数量超过限制。");

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ContentFileEntry file in manifest.Files)
            {
                ValidatePath(file.Path);
                if (!paths.Add(file.Path)) throw new ContentManifestFormatException($"清单包含重复路径：{file.Path}。");
                if (file.Length < 0 || file.Sha256.Length != 64 || file.Sha256.Any(character => !IsLowerHex(character)))
                    throw new ContentManifestFormatException($"清单文件长度或 SHA-256 值无效：{file.Path}。");
            }

            var keyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ContentKeyRotation rotation in manifest.Rotations)
            {
                ValidateIdentifier(rotation.KeyId, nameof(rotation.KeyId));
                if (!keyIds.Add(rotation.KeyId) || rotation.ActivateAtSequence <= manifest.Sequence || rotation.RetireAtSequence < rotation.ActivateAtSequence)
                    throw new ContentManifestFormatException($"密钥轮换区间或标识无效：{rotation.KeyId}。");
                if (rotation.RetireKeyId != null)
                {
                    ValidateIdentifier(rotation.RetireKeyId, nameof(rotation.RetireKeyId));
                    if (rotation.RetireKeyId == rotation.KeyId || rotation.RetireExistingKeyAtSequence == null || rotation.RetireExistingKeyAtSequence.Value <= manifest.Sequence)
                        throw new ContentManifestFormatException($"待退役密钥或退役序号无效：{rotation.KeyId}。");
                }
                else if (rotation.RetireExistingKeyAtSequence != null)
                {
                    throw new ContentManifestFormatException("指定退役序号时必须同时指定待退役密钥。");
                }
                byte[] publicKey = rotation.SubjectPublicKeyInfo;
                if (publicKey.Length == 0 || publicKey.Length > 512) throw new ContentManifestFormatException("密钥轮换公钥长度无效。");
            }
        }

        private static void ValidateIdentifier(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
                throw new ContentManifestFormatException($"清单字段 {field} 无效。");
            foreach (char character in value)
            {
                if (!(character >= 'a' && character <= 'z') && !(character >= 'A' && character <= 'Z') &&
                    !(character >= '0' && character <= '9') && character != '.' && character != '_' && character != '-')
                    throw new ContentManifestFormatException($"清单字段 {field} 必须使用 ASCII 标识字符。");
            }
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length > 1024 || path[0] == '/' || path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0)
                throw new ContentManifestFormatException($"清单文件路径无效：{path}。");
            foreach (string segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..") throw new ContentManifestFormatException($"清单文件路径无效：{path}。");
                foreach (char character in segment)
                {
                    if (!(character >= 'a' && character <= 'z') && !(character >= 'A' && character <= 'Z') &&
                        !(character >= '0' && character <= '9') && character != '.' && character != '_' && character != '-')
                        throw new ContentManifestFormatException($"清单文件路径必须使用 ASCII 路径字符：{path}。");
                }
            }
        }

        private static bool IsLowerHex(char value) => value >= '0' && value <= '9' || value >= 'a' && value <= 'f';
        private static void WriteString(Stream stream, string value) => WriteBytes(stream, Utf8.GetBytes(value));
        private static void WriteBytes(Stream stream, byte[] value) { WriteInt32(stream, value.Length); stream.Write(value, 0, value.Length); }
        private static void WriteInt32(Stream stream, int value)
        {
            stream.WriteByte((byte)(value >> 24)); stream.WriteByte((byte)(value >> 16)); stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value);
        }
        private static void WriteInt64(Stream stream, long value)
        {
            for (int shift = 56; shift >= 0; shift -= 8) stream.WriteByte((byte)(value >> shift));
        }
    }
}
