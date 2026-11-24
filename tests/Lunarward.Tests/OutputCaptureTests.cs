using Starward.Features.Runtime;
using Xunit;

namespace Lunarward.Tests;

/// <summary>
/// 输出面板的脱敏（R106）：以前替换的是关键词本身，凭证原样留在屏幕上。
/// </summary>
public class OutputCaptureTests
{
    [Theory]
    [InlineData("token=abc123", "token=***")]
    [InlineData("Authorization: Bearer xyz", "Authorization=***")]
    [InlineData("api_key: 0123456789", "api_key=***")]
    [InlineData("password=hunter2 run", "password=*** run")]
    public void Redact_MasksValuesAfterSecrets(string input, string expected)
    {
        Assert.Equal(expected, OutputCapture.Redact(input));
    }

    [Theory]
    [InlineData("Authenticating with the store...")]
    [InlineData("no secrets here, just words: token")]
    [InlineData("file=tokens.txt")]
    public void Redact_LeavesNonCredentialsAlone(string input)
    {
        Assert.Equal(input, OutputCapture.Redact(input));
    }
}
