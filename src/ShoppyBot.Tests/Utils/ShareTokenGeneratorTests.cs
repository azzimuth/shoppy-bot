using Xunit;
using ShoppyBot.Utils;

namespace ShoppyBot.Tests.Utils;

public class ShareTokenGeneratorTests
{
    [Fact]
    public void GenerateToken_ReturnsTokenOfCorrectLength()
    {
        var token = ShareTokenGenerator.GenerateToken();

        Assert.Equal(32, token.Length);
    }

    [Fact]
    public void GenerateToken_ReturnsUrlSafeToken()
    {
        var token = ShareTokenGenerator.GenerateToken();

        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void GenerateToken_ReturnsUniqueTokens()
    {
        var tokens = new HashSet<string>();

        for (int i = 0; i < 100; i++)
        {
            var token = ShareTokenGenerator.GenerateToken();
            Assert.True(tokens.Add(token), "Generated duplicate token");
        }
    }

    [Fact]
    public void IsValidToken_ReturnsTrue_ForValidToken()
    {
        var token = ShareTokenGenerator.GenerateToken();

        Assert.True(ShareTokenGenerator.IsValidToken(token));
    }

    [Fact]
    public void IsValidToken_ReturnsFalse_ForNullToken()
    {
        Assert.False(ShareTokenGenerator.IsValidToken(null));
    }

    [Fact]
    public void IsValidToken_ReturnsFalse_ForEmptyToken()
    {
        Assert.False(ShareTokenGenerator.IsValidToken(""));
    }

    [Fact]
    public void IsValidToken_ReturnsFalse_ForShortToken()
    {
        Assert.False(ShareTokenGenerator.IsValidToken("abc123"));
    }

    [Fact]
    public void IsValidToken_ReturnsFalse_ForTokenWithInvalidChars()
    {
        Assert.False(ShareTokenGenerator.IsValidToken("abc+def/ghi=jklmnopqrstuvwxyz12"));
    }

    [Fact]
    public void IsValidToken_ReturnsTrue_ForTokenWithHyphensAndUnderscores()
    {
        var token = "abcdefghijklmnop-qrstuvwxyz_12345";
        Assert.False(ShareTokenGenerator.IsValidToken(token));

        var validToken = "abcdefghijklmnop-qrstuv_xyz12345";
        Assert.True(ShareTokenGenerator.IsValidToken(validToken));
    }
}
