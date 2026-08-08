using System.Data;
using Moq;
using RmsSupportHub.Data;
using RmsSupportHub.Data.Repositories;
using Xunit;

namespace RmsSupportHub.Tests;

public class RepositoryTests
{
    [Fact]
    public async Task FlatOrderItemRepository_InvalidMaterialNumber_ThrowsArgumentException()
    {
        var mockConnFactory = new Mock<ISqlServerConnectionFactory>();
        var repo = new FlatOrderItemRepository(mockConnFactory.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => repo.LookupItemAsync("FakeConnStr", "123")); // not 6 digits
        await Assert.ThrowsAsync<ArgumentException>(() => repo.LookupItemAsync("FakeConnStr", "ABCDEF")); // not digits
    }

    [Fact]
    public async Task UpcItemRepository_EmptySearchCode_ThrowsArgumentException()
    {
        var mockConnFactory = new Mock<ISqlServerConnectionFactory>();
        var repo = new UpcItemRepository(mockConnFactory.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => repo.LookupItemAsync("FakeConnStr", "   "));
    }

    [Fact]
    public async Task GhcConsumerRepository_EmptyPhone_ThrowsArgumentException()
    {
        var mockConnFactory = new Mock<ISqlServerConnectionFactory>();
        var repo = new GhcConsumerRepository(mockConnFactory.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => repo.LookupConsumerByPhoneAsync("FakeConnStr", ""));
    }

    [Fact]
    public async Task UpcConsumerRepository_EmptyPhone_ThrowsArgumentException()
    {
        var mockConnFactory = new Mock<ISqlServerConnectionFactory>();
        var repo = new UpcConsumerRepository(mockConnFactory.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => repo.LookupConsumerByPhoneAsync("FakeConnStr", "  "));
    }
}
