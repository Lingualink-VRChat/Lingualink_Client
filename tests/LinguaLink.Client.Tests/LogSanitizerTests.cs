using lingualink_client.Services;
using Xunit;

namespace LinguaLink.Client.Tests;

public class LogSanitizerTests
{
    [Fact]
    public void SanitizeJsonPayload_RedactsTokensAudioAndUserText()
    {
        const string payload = """
            {
              "access_token": "secret-access",
              "refresh_token": "secret-refresh",
              "audio": "abcdef",
              "text": "hello private text",
              "target_languages": ["en", "ja"]
            }
            """;

        var result = LogSanitizer.SanitizeJsonPayload(payload);

        Assert.DoesNotContain("secret-access", result);
        Assert.DoesNotContain("secret-refresh", result);
        Assert.DoesNotContain("abcdef", result);
        Assert.DoesNotContain("hello private text", result);
        Assert.Contains("\"target_languages\":[\"en\",\"ja\"]", result);
        Assert.Contains("[redacted:access_token", result);
        Assert.Contains("[redacted:audio", result);
    }

    [Fact]
    public void SanitizeJsonPayload_RedactsTranslationValuesButKeepsLanguageKeys()
    {
        const string payload = """
            {
              "request_id": "req-1",
              "translations": {
                "en": "private translation",
                "ja": "ひみつ"
              }
            }
            """;

        var result = LogSanitizer.SanitizeJsonPayload(payload);

        Assert.Contains("req-1", result);
        Assert.Contains("\"en\"", result);
        Assert.Contains("\"ja\"", result);
        Assert.DoesNotContain("private translation", result);
        Assert.DoesNotContain("ひみつ", result);
    }

    [Fact]
    public void SummarizeUrl_KeepsPathAndQueryNamesOnly()
    {
        var result = LogSanitizer.SummarizeUrl("https://auth.example.com/auth?client_callback=http%3A%2F%2Flocalhost&token=secret");

        Assert.Equal("https://auth.example.com/auth?[client_callback,token]", result);
        Assert.DoesNotContain("secret", result);
        Assert.DoesNotContain("localhost", result);
    }

    [Fact]
    public void SanitizeJsonPayload_RedactsCamelCaseSensitiveKeys()
    {
        const string payload = """
            {
              "accessToken": "camel-access",
              "refreshToken": "camel-refresh",
              "rawResponse": "private response"
            }
            """;

        var result = LogSanitizer.SanitizeJsonPayload(payload);

        Assert.DoesNotContain("camel-access", result);
        Assert.DoesNotContain("camel-refresh", result);
        Assert.DoesNotContain("private response", result);
    }

    [Fact]
    public void SanitizeJsonPayload_RedactsInvalidPayloadAsWholeValue()
    {
        var result = LogSanitizer.SanitizeJsonPayload("plain secret response");

        Assert.Equal("[redacted:payload, chars=21]", result);
    }
}
