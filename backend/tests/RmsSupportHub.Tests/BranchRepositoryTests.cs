using RmsSupportHub.Data.Repositories;
using Xunit;

namespace RmsSupportHub.Tests;

public class BranchRepositoryTests
{
    [Fact]
    public void ListBranchesSql_OrdersByDatabaseBranchId()
    {
        Assert.Contains("ORDER BY B.Id", BranchRepository.ListBranchesSql);
        Assert.DoesNotContain("ORDER BY B.Name", BranchRepository.ListBranchesSql);
    }
}
