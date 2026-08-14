using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;
using RmsSupportHub.Pos.Agent.MutationTokens;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class MutationTokenTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    private const string Sid = FakeAuthenticationHandler.DefaultSid;
    private const string Origin = AgentWebApplicationFactory.SupportHubOrigin;

    [Fact]
    public void IssueAndConsume_IsOneUseAndOperationBound()
    {
        var store = CreateStore();
        var issue = store.Issue(Sid, Origin, "POST", "services.restart");

        var consumed = store.TryConsume(new MutationTokenValidationRequest(
            issue.Token, Sid, Origin, "POST", "services.restart"));
        var replay = store.TryConsume(new MutationTokenValidationRequest(
            issue.Token, Sid, Origin, "POST", "services.restart"));

        Assert.True(consumed.Succeeded);
        Assert.Equal(MutationTokenFailure.Unknown, replay.Failure);
    }

    [Theory]
    [InlineData("S-1-5-21-1111111111-2222222222-3333333333-1002", "https://support-hub.integration.test", "POST", "services.restart")]
    [InlineData(Sid, "https://other.example.test", "POST", "services.restart")]
    [InlineData(Sid, Origin, "GET", "services.restart")]
    [InlineData(Sid, Origin, "POST", "services.stop")]
    public void BindingMismatch_ConsumesTheTokenAndFailsClosed(string sid, string origin, string method, string operationId)
    {
        var store = CreateStore();
        var issue = store.Issue(Sid, Origin, "POST", "services.restart");

        var mismatch = store.TryConsume(new MutationTokenValidationRequest(issue.Token, sid, origin, method, operationId));
        var replay = store.TryConsume(new MutationTokenValidationRequest(issue.Token, Sid, Origin, "POST", "services.restart"));

        Assert.Equal(MutationTokenFailure.Mismatch, mismatch.Failure);
        Assert.Equal(MutationTokenFailure.Unknown, replay.Failure);
    }

    [Fact]
    public void ExpiredToken_IsRejectedAndRemoved()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new InMemoryMutationTokenStore(clock, new MutationTokenOptions
        {
            MaxTokens = 2,
            Lifetime = TimeSpan.FromMinutes(1)
        });
        var issue = store.Issue(Sid, Origin, "POST", "configuration.update");

        clock.Advance(TimeSpan.FromMinutes(1));

        var expired = store.TryConsume(new MutationTokenValidationRequest(
            issue.Token, Sid, Origin, "POST", "configuration.update"));

        Assert.Equal(MutationTokenFailure.Expired, expired.Failure);
    }

    [Fact]
    public void DefaultOptions_LifetimeIsSixtySeconds()
    {
        var options = new MutationTokenOptions();

        Assert.Equal(TimeSpan.FromSeconds(60), options.Lifetime);
    }

    [Fact]
    public void TokenIssuedWithProductionDefaultLifetime_IsValidJustBeforeExpiry()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new InMemoryMutationTokenStore(clock, new MutationTokenOptions());
        var issue = store.Issue(Sid, Origin, "POST", "services.restart");

        clock.Advance(TimeSpan.FromSeconds(59));

        var stillValid = store.TryConsume(new MutationTokenValidationRequest(
            issue.Token, Sid, Origin, "POST", "services.restart"));

        Assert.True(stillValid.Succeeded);
    }

    [Fact]
    public void TokenIssuedWithProductionDefaultLifetime_IsExpiredAtSixtySeconds()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new InMemoryMutationTokenStore(clock, new MutationTokenOptions());
        var issue = store.Issue(Sid, Origin, "POST", "services.restart");

        clock.Advance(TimeSpan.FromSeconds(60));

        var expired = store.TryConsume(new MutationTokenValidationRequest(
            issue.Token, Sid, Origin, "POST", "services.restart"));

        Assert.Equal(MutationTokenFailure.Expired, expired.Failure);
    }

    [Fact]
    public void CapacityIsBoundedAndDoesNotEvictValidTokens()
    {
        var store = new InMemoryMutationTokenStore(new ManualTimeProvider(Start), new MutationTokenOptions
        {
            MaxTokens = 1,
            Lifetime = TimeSpan.FromMinutes(1)
        });
        var first = store.Issue(Sid, Origin, "POST", "backup.create");

        Assert.Throws<MutationTokenCapacityException>(() => store.Issue(Sid, Origin, "POST", "backup.create"));
        Assert.True(store.TryConsume(new MutationTokenValidationRequest(
            first.Token, Sid, Origin, "POST", "backup.create")).Succeeded);
    }

    [Fact]
    public void MissingTokenAndInvalidPrincipalFailClosed()
    {
        var store = CreateStore();

        var missing = store.TryConsume(new MutationTokenValidationRequest(string.Empty, Sid, Origin, "POST", "op"));
        var invalidPrincipal = store.TryConsume(new MutationTokenValidationRequest("opaque", "display-name", Origin, "POST", "op"));

        Assert.Equal(MutationTokenFailure.Missing, missing.Failure);
        Assert.Equal(MutationTokenFailure.Mismatch, invalidPrincipal.Failure);
    }

    [Fact]
    public void ANewStoreCannotConsumeATokenFromAnOlderStore()
    {
        var firstStore = CreateStore();
        var issue = firstStore.Issue(Sid, Origin, "POST", "maintenance.preview");
        var restartedStore = CreateStore();

        var result = restartedStore.TryConsume(new MutationTokenValidationRequest(
            issue.Token, Sid, Origin, "POST", "maintenance.preview"));

        Assert.Equal(MutationTokenFailure.Unknown, result.Failure);
    }

    private static InMemoryMutationTokenStore CreateStore() =>
        new(new ManualTimeProvider(Start), new MutationTokenOptions
        {
            MaxTokens = 8,
            Lifetime = TimeSpan.FromMinutes(5)
        });
}
