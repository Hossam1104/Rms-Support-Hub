using RmsSupportHub.Pos.Infrastructure.Diagnostics;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class DiagnosticRedactorTests
{
    [Fact]
    public void RedactSummaryRemovesSecretsIdentitiesPathsAndKeyMaterial()
    {
        var value =
            "Data Source=sql.synthetic.test;Database=RmsBranchSrv;User ID=reader;Password=super-secret; " +
            "Bearer bearer-secret token=token-secret ApiKey=api-secret " +
            "https://main.synthetic.test/api?password=url-secret " +
            @"C:\ProgramData\RMS_Plus\private.pfx DOMAIN\operator S-1-5-21-100-200-300-400 " +
            "DPAPI=dpapi-secret PFX=pfx-secret";

        var redacted = DiagnosticRedactor.RedactSummary(value);

        Assert.DoesNotContain("super-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("bearer-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("token-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("api-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("url-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("ProgramData", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("operator", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("S-1-5-21", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("dpapi-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("pfx-secret", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactionCoversStructuredSecretsAndTruncatedKeyMaterial()
    {
        var value = """
            <add key="Password" value="xml-secret" />
            <add name="apiKey" value='xml-api-secret' />
            {"password":"json-secret","apiKey":"json-api-secret","access_token":"json-token-secret","authorization":"Bearer json-bearer-secret"}
            Server=sql.synthetic.test;Password=sql-secret;User ID=sql-user;
            Authorization: Bearer bearer-secret token=token-secret ApiKey=api-secret
            https://main.synthetic.test/api?client=one&password=url-secret
            """;
        var redacted = new DiagnosticOutputRedactor().Redact(value);

        foreach (var secret in new[]
        {
            "xml-secret",
            "xml-api-secret",
            "json-secret",
            "json-api-secret",
            "json-token-secret",
            "json-bearer-secret",
            "sql-secret",
            "bearer-secret",
            "token-secret",
            "api-secret",
            "url-secret"
        })
        {
            Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        }

        var truncatedPem = new DiagnosticOutputRedactor().Redact("-----BEGIN RSA PRIVATE KEY-----\ntruncated-private-key");
        Assert.DoesNotContain("BEGIN RSA PRIVATE KEY", truncatedPem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("truncated-private-key", truncatedPem, StringComparison.Ordinal);
        Assert.InRange(redacted.Length, 0, 64 * 1024);
    }

    [Fact]
    public void StackFramesRemainBoundedAndKeepOnlyFrameLabels()
    {
        var frames = DiagnosticRedactor.StackFrames(
        [
            "at RMS.Branch.Worker.Run() in C:\\ProgramData\\RMS_Plus\\worker.cs:line 42",
            "not a frame",
            "at RMS.Branch.Worker.Run() in C:\\ProgramData\\RMS_Plus\\worker.cs:line 42"
        ]);

        Assert.Single(frames);
        Assert.StartsWith("at ", frames[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProgramData", frames[0], StringComparison.OrdinalIgnoreCase);
    }
}
