using MiJia.Net.Security;
using Xunit;

namespace MiJia.Net.Tests;

public class MiSignerTests
{
    [Fact]
    public void GenerateNonce_ShouldHaveValidFormat()
    {
        string nonce = MiSigner.GenerateNonce();
        
        Assert.NotNull(nonce);

        // 验证整体是合法的 Base64 编码（且解码后长度大于等于 9 字节：8 字节随机数 + 时间戳）
        byte[] bytes = Convert.FromBase64String(nonce);
        Assert.True(bytes.Length >= 9);
    }

    [Fact]
    public void GenerateSignedNonce_ShouldComputeDeterministicSha256()
    {
        // 模拟已知的 ssecurity (Base64) 与固定 nonce (Base64)
        byte[] rawSecret = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        string ssecurity = Convert.ToBase64String(rawSecret);
        byte[] rawNonce = new byte[12] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 1, 2 };
        string nonce = Convert.ToBase64String(rawNonce);

        string signedNonce1 = MiSigner.GenerateSignedNonce(ssecurity, nonce);
        string signedNonce2 = MiSigner.GenerateSignedNonce(ssecurity, nonce);

        Assert.NotNull(signedNonce1);
        Assert.Equal(signedNonce1, signedNonce2);

        // 派生的 signed_nonce 应该是 32 字节哈希的 Base64 串
        byte[] hashBytes = Convert.FromBase64String(signedNonce1);
        Assert.Equal(32, hashBytes.Length);
    }

    [Fact]
    public void GenerateSignature_ShouldGenerateValidHmac()
    {
        byte[] rawSignedKey = new byte[32];
        Array.Fill<byte>(rawSignedKey, 0xAB);
        string signedNonce = Convert.ToBase64String(rawSignedKey);
        string nonce = "fake_nonce:123";
        string path = "/app/home/device_list";
        string data = "{\"fg\":true}";

        string sig1 = MiSigner.GenerateSignature(path, signedNonce, nonce, data);
        string sig2 = MiSigner.GenerateSignature(path, signedNonce, nonce, data);

        Assert.False(string.IsNullOrEmpty(sig1));
        Assert.Equal(sig1, sig2);

        // 改变任意参数，签名必须改变
        string sigDifferent = MiSigner.GenerateSignature(path, signedNonce, nonce, "{\"fg\":false}");
        Assert.NotEqual(sig1, sigDifferent);
    }
}
