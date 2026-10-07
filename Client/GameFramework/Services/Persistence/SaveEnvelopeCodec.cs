using System;
using System.Security.Cryptography;
using System.Text;

namespace Lyking.GameFramework.Client.Services.Persistence
{
    /// <summary>固定二进制格式：魔数、信封版本、数据版本、保护后长度、数据和 SHA-256 校验值。</summary>
    public static class SaveEnvelopeCodec
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("LYSV");
        private const int HeaderLength = 14;
        private const int DigestLength = 32;

        public static byte[] Encode(string key, int payloadVersion, byte[] payload, ISaveDataProtector protector)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("存档键不能为空。", nameof(key));
            if (payloadVersion <= 0) throw new ArgumentOutOfRangeException(nameof(payloadVersion));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (protector == null) throw new ArgumentNullException(nameof(protector));

            byte[] protectedData = protector.Protect(key, (byte[])payload.Clone()) ?? throw new SaveDataIntegrityException("存档保护器返回空数据。");
            var envelope = new byte[HeaderLength + protectedData.Length + DigestLength];
            Buffer.BlockCopy(Magic, 0, envelope, 0, Magic.Length);
            envelope[4] = 1;
            envelope[5] = 0;
            WriteInt32(envelope, 6, payloadVersion);
            WriteInt32(envelope, 10, protectedData.Length);
            Buffer.BlockCopy(protectedData, 0, envelope, HeaderLength, protectedData.Length);
            byte[] digest;
            using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(envelope, 0, envelope.Length - DigestLength);
            Buffer.BlockCopy(digest, 0, envelope, envelope.Length - DigestLength, DigestLength);
            return envelope;
        }

        public static int ReadPayloadVersion(byte[] envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            ValidateEnvelope(envelope);
            return ReadInt32(envelope, 6);
        }

        internal static byte[] Decode(string key, byte[] envelope, ISaveDataProtector protector, out int payloadVersion)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            if (protector == null) throw new ArgumentNullException(nameof(protector));
            ValidateEnvelope(envelope);
            payloadVersion = ReadInt32(envelope, 6);
            int dataLength = ReadInt32(envelope, 10);
            var protectedData = new byte[dataLength];
            Buffer.BlockCopy(envelope, HeaderLength, protectedData, 0, dataLength);
            try
            {
                return protector.Unprotect(key, protectedData) ?? throw new SaveDataIntegrityException("存档保护器返回空明文。");
            }
            catch (SaveDataException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new SaveDataIntegrityException("存档保护数据无法解封。", exception);
            }
        }

        private static void ValidateEnvelope(byte[] envelope)
        {
            if (envelope.Length < HeaderLength + DigestLength ||
                envelope[0] != Magic[0] || envelope[1] != Magic[1] || envelope[2] != Magic[2] || envelope[3] != Magic[3])
            {
                throw new SaveDataIntegrityException("存档信封头损坏或格式不受支持。");
            }

            if (envelope[4] != 1 || envelope[5] != 0)
            {
                throw new SaveDataIntegrityException("存档信封格式版本不受支持。");
            }

            int version = ReadInt32(envelope, 6);
            int dataLength = ReadInt32(envelope, 10);
            if (version <= 0 || dataLength < 0 || envelope.Length != HeaderLength + dataLength + DigestLength)
            {
                throw new SaveDataIntegrityException("存档信封长度或数据版本无效。");
            }

            byte[] digest;
            using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(envelope, 0, envelope.Length - DigestLength);
            int difference = 0;
            for (int index = 0; index < DigestLength; index++)
            {
                difference |= digest[index] ^ envelope[envelope.Length - DigestLength + index];
            }
            if (difference != 0)
            {
                throw new SaveDataIntegrityException("存档完整性校验失败。");
            }
        }

        private static int ReadInt32(byte[] bytes, int offset) =>
            bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24;

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }
    }
}
