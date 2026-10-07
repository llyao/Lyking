using System.Security.Cryptography;
using System.Text;
using Lyking.GameFramework.Client.Services.ContentUpdate;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

public sealed class ContentManifestSignatureTests
{
    [Fact]
    public void 清单签名固定使用64字节P1363编码()
    {
        byte[] data = Encoding.UTF8.GetBytes("lyking-client-manifest-format-v1");
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var trustedKey = ECDsa.Create();
        trustedKey.ImportSubjectPublicKeyInfo(signer.ExportSubjectPublicKeyInfo(), out _);
        var trustStore = new EcdsaContentManifestTrustStore();
        trustStore.AddTrustedRoot("root", trustedKey);

        byte[] signature = signer.SignData(data, HashAlgorithmName.SHA256);

        Assert.Equal(64, signature.Length);
        Assert.True(trustStore.VerifySignature("root", 1, data, signature));
        Assert.False(trustStore.VerifySignature("root", 1, data, new byte[70]));
    }

    [Fact]
    public void 信任根拒绝其它256位椭圆曲线()
    {
        using var alternateCurveKey = ECDsa.Create(ECCurve.CreateFromOid(new Oid("1.3.132.0.10")));
        Assert.Equal(256, alternateCurveKey.KeySize);

        var trustStore = new EcdsaContentManifestTrustStore();

        Assert.Throws<ArgumentException>(() => trustStore.AddTrustedRoot("alternate", alternateCurveKey));
    }

    [Fact]
    public void 轮换密钥拒绝其它256位椭圆曲线()
    {
        using var alternateCurveKey = ECDsa.Create(ECCurve.CreateFromOid(new Oid("1.3.132.0.10")));
        Assert.Equal(256, alternateCurveKey.KeySize);
        var trustStore = new EcdsaContentManifestTrustStore();
        var rotation = new ContentKeyRotation("alternate", alternateCurveKey.ExportSubjectPublicKeyInfo(), 2, 10);

        Assert.Throws<ContentManifestFormatException>(() => trustStore.PrepareRotations(new[] { rotation }));
    }
}
