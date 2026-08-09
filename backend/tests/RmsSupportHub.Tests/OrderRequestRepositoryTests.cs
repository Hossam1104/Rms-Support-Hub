using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Data.Repositories;
using Xunit;

namespace RmsSupportHub.Tests;

/// <summary>Asserts on the SQL shape OrderRequestRepository builds, without a
/// live database -- BuildListSql/BuildStatsSql/BuildFilters are internal, exposed to this
/// assembly via InternalsVisibleTo in RmsSupportHub.Data.csproj.</summary>
public class OrderRequestRepositoryTests
{
    [Fact]
    public void ListSql_UsesOuterApply_NotPlainJoin()
    {
        var sql = OrderRequestRepository.BuildListSql("", null);
        Assert.Contains("OUTER APPLY", sql);
        Assert.DoesNotContain(" JOIN dbo.RequestOrderHeaders", sql);
        Assert.DoesNotContain(" JOIN dbo.Invoices", sql);
    }

    [Fact]
    public void ListSql_IsBasedOnOrderRequests()
    {
        var sql = OrderRequestRepository.BuildListSql("", null);
        Assert.Contains("FROM dbo.OrderRequests AS R", sql);
    }

    [Fact]
    public void ListSql_FastPathPagesBaseRowsBeforeHeaderAndInvoiceLookups()
    {
        var sql = OrderRequestRepository.BuildListSql("", null, applyHeaderJoinsAfterPaging: true);

        Assert.Contains("WITH PagedRequests AS", sql);
        Assert.Contains("FROM PagedRequests AS R", sql);
        Assert.Contains("OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY", sql);
        Assert.True(sql.IndexOf("OFFSET @Skip", StringComparison.Ordinal)
            < sql.IndexOf("OUTER APPLY", StringComparison.Ordinal));
    }

    [Fact]
    public void ListSql_NeverSelectsTheRawBlobs_OnlyLengthAndExistence()
    {
        var sql = OrderRequestRepository.BuildListSql("", null);

        Assert.Contains("DATALENGTH(R.RequestJson)", sql);
        Assert.Contains("CASE WHEN R.ResponseJson IS NULL", sql);

        // Strip the two sanctioned usages (length check, null check) and
        // confirm neither blob column name appears anywhere else in the
        // query -- i.e. it is never selected as a raw output column.
        var remainder = sql
            .Replace("DATALENGTH(R.RequestJson)", "")
            .Replace("CASE WHEN R.ResponseJson IS NULL THEN 0 ELSE 1 END", "");

        Assert.DoesNotContain("RequestJson", remainder);
        Assert.DoesNotContain("ResponseJson", remainder);
    }

    [Fact]
    public void ListSql_PagesWithBoundParameters_NotInterpolatedLiterals()
    {
        var sql = OrderRequestRepository.BuildListSql("", null);

        Assert.Contains("OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY", sql);
        // No literal "OFFSET <number> ROWS" -- proves paging is bound as
        // parameters, not string-substituted into the SQL text.
        Assert.DoesNotMatch(@"OFFSET\s+\d+\s+ROWS", sql);
    }

    [Fact]
    public void ListSql_OrdersByIdAsStableTieBreaker()
    {
        var sql = OrderRequestRepository.BuildListSql("", null);
        Assert.Contains("R.Id DESC", sql);
        // The tie-breaker must be the last thing before OFFSET, i.e. applied
        // regardless of which column sorts first.
        var orderByIndex = sql.IndexOf("ORDER BY", StringComparison.Ordinal);
        var offsetIndex = sql.IndexOf("OFFSET", StringComparison.Ordinal);
        var idDescIndex = sql.IndexOf("R.Id DESC", StringComparison.Ordinal);
        Assert.InRange(idDescIndex, orderByIndex, offsetIndex);
    }

    [Fact]
    public void BuildFilters_EmptyFilters_ProducesNoWhereClauseAndNoParams()
    {
        var (whereSql, p) = OrderRequestRepository.BuildFilters(new OrderRequestFilters());
        Assert.Equal("", whereSql);
        Assert.Empty(p.ParameterNames);
    }

    [Fact]
    public void BuildFilters_WhitespaceAndEmptyOptionalValues_AreEquivalentToOmittedValues()
    {
        var omitted = OrderRequestRepository.BuildFilters(new OrderRequestFilters());
        var empty = OrderRequestRepository.BuildFilters(new OrderRequestFilters(
            OrderNumber: "  ",
            Phone: " \t",
            BranchCode: " ",
            Statuses: Array.Empty<int>(),
            DateFrom: null,
            DateTo: null));

        Assert.Equal(omitted.WhereSql, empty.WhereSql);
        Assert.Empty(empty.Params.ParameterNames);
    }

    [Fact]
    public void BuildFilters_BindsEveryValueAsAParameter_NeverInterpolatesIt()
    {
        var filters = new OrderRequestFilters(
            OrderNumber: "ORD-1; DROP TABLE OrderRequests;--",
            Phone: "0556028080",
            BranchCode: "P001",
            Status: 5,
            Succeeded: true,
            HasException: true,
            DateFrom: new DateTime(2026, 1, 1),
            DateTo: new DateTime(2026, 1, 31));

        var (whereSql, p) = OrderRequestRepository.BuildFilters(filters);

        Assert.Contains("@OrderNumber", whereSql);
        Assert.Contains("@Phone9", whereSql);
        Assert.Contains("@BranchCode", whereSql);
        Assert.Contains("@Status", whereSql);
        Assert.Contains("@Succeeded", whereSql);
        Assert.Contains("@DateFrom", whereSql);
        Assert.Contains("@DateTo", whereSql);

        // The malicious order-number value must never appear literally in
        // the SQL text -- only ever as a bound parameter value.
        Assert.DoesNotContain("DROP TABLE", whereSql);
        Assert.Equal("ORD-1; DROP TABLE OrderRequests;--", (string)p.Get<object>("OrderNumber"));

        // Phone is normalized to its last 9 digits before binding.
        Assert.Equal("556028080", (string)p.Get<object>("Phone9"));
    }

    [Fact]
    public void ListSql_ProjectsConsumerMobileForPhoneFiltering()
    {
        var sql = OrderRequestRepository.BuildListSql("WHERE RIGHT(H.ConsumerMobile, 9) = @Phone9", null);

        Assert.Contains("ConsumerMobile", sql);
    }

    [Fact]
    public void BuildFilters_Statuses_ProducesInClauseAndTakesPrecedenceOverSingleStatus()
    {
        var filters = new OrderRequestFilters(Status: 9, Statuses: new[] { 6, 7 });

        var (whereSql, p) = OrderRequestRepository.BuildFilters(filters);

        Assert.Contains("H.OrderStatus IN @Statuses", whereSql);
        Assert.DoesNotContain("@Status ", whereSql); // the single-value form must not also be bound
        Assert.Equal(new[] { 6, 7 }, (IEnumerable<int>)p.Get<object>("Statuses"));
    }

    [Fact]
    public void BuildFilters_SingleStatus_UsedWhenStatusesIsAbsent()
    {
        var filters = new OrderRequestFilters(Status: 9);

        var (whereSql, p) = OrderRequestRepository.BuildFilters(filters);

        Assert.Contains("H.OrderStatus = @Status", whereSql);
        Assert.Equal(9, p.Get<object>("Status"));
    }

    [Fact]
    public void BuildFilters_HasExceptionFalse_FiltersOnIsNullNotAParameter()
    {
        var (whereSql, _) = OrderRequestRepository.BuildFilters(new OrderRequestFilters(HasException: false));
        Assert.Contains("R.ExceptionMessage IS NULL", whereSql);
    }

    [Fact]
    public void BuildFilters_PartialOrderNumber_UsesEscapedContainsParameter()
    {
        var filters = new OrderRequestFilters(OrderNumber: "UPC_%[42]", ExactOrderNumber: false);

        var (whereSql, parameters) = OrderRequestRepository.BuildFilters(filters);

        Assert.Contains("R.OrderNumber LIKE @OrderNumberPattern ESCAPE '\\'", whereSql);
        Assert.DoesNotContain("UPC_%[42]", whereSql);
        Assert.Equal("%UPC\\_\\%\\[42]%", (string)parameters.Get<object>("OrderNumberPattern"));
    }

    [Fact]
    public void BuildFilters_FailedOutcome_MatchesTheStatsDefinition()
    {
        var (whereSql, _) = OrderRequestRepository.BuildFilters(new OrderRequestFilters(Succeeded: false));

        Assert.Contains("(R.IsSucceeded = @Succeeded OR R.ExceptionMessage IS NOT NULL)", whereSql);
    }

    [Fact]
    public void HeaderFilteredList_UsesOneRankedHeaderRowBeforePaging()
    {
        var sql = OrderRequestRepository.BuildListSql("WHERE H.BranchCode = @BranchCode", null);

        Assert.Contains("WITH LatestHeaders AS", sql);
        Assert.Contains("ROW_NUMBER() OVER", sql);
        Assert.Contains("LEFT JOIN LatestHeaders AS H", sql);
        Assert.Contains("PARTITION BY H.OrderNumber", sql);
        Assert.Contains("OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY", sql);
        Assert.DoesNotContain("JOIN dbo.RequestOrderHeaders AS H", sql);
    }

    [Fact]
    public void CountAndStats_UseDistinctBaseRequestIdsAndTheSameLatestHeaderSet()
    {
        var countSql = OrderRequestRepository.BuildCountSql("WHERE H.OrderStatus IN @Statuses", requiresHeaderJoin: true);
        var statsSql = OrderRequestRepository.BuildStatsSql("WHERE H.OrderStatus IN @Statuses");

        Assert.Contains("COUNT(DISTINCT R.Id)", countSql);
        Assert.Contains("COUNT(DISTINCT R.Id)", statsSql);
        Assert.Contains("WITH LatestHeaders AS", countSql);
        Assert.Contains("WITH LatestHeaders AS", statsSql);
        Assert.Contains("LEFT JOIN LatestHeaders AS H", countSql);
        Assert.Contains("LEFT JOIN LatestHeaders AS H", statsSql);
        Assert.Contains("H.OrderStatus IN @Statuses", countSql);
        Assert.Contains("H.OrderStatus IN @Statuses", statsSql);
    }

    [Fact]
    public void CountWithoutHeaderFilters_StaysOnTheBaseTable()
    {
        var sql = OrderRequestRepository.BuildCountSql("WHERE R.OrderDate >= @DateFrom", requiresHeaderJoin: false);

        Assert.Contains("FROM dbo.OrderRequests AS R", sql);
        Assert.DoesNotContain("LatestHeaders", sql);
        Assert.Contains("COUNT(DISTINCT R.Id)", sql);
    }

    [Fact]
    public void LatestUnfilteredCountSql_OnlyCountsTenNewestRequests()
    {
        var sql = OrderRequestRepository.BuildCountSql(
            "",
            requiresHeaderJoin: false,
            latestUnfilteredOnly: true);

        Assert.Contains("SELECT TOP (10) R.Id", sql);
        Assert.Contains("ORDER BY R.Id DESC", sql);
        Assert.Contains("SELECT COUNT(*)", sql);
    }

    [Fact]
    public void StatsWithBaseFilter_NarrowsRequestsBeforeLatestHeaderLookup()
    {
        var sql = OrderRequestRepository.BuildStatsSql(
            "WHERE R.OrderNumber = @OrderNumber",
            useFilteredBaseRows: true);

        Assert.Contains("WITH FilteredRequests AS", sql);
        Assert.Contains("FROM dbo.OrderRequests AS R", sql);
        Assert.Contains("FROM FilteredRequests AS R", sql);
        Assert.DoesNotContain("WITH LatestHeaders AS", sql);
        Assert.True(sql.IndexOf("WHERE R.OrderNumber", StringComparison.Ordinal)
            < sql.IndexOf("OUTER APPLY", StringComparison.Ordinal));
    }

    [Fact]
    public void LatestUnfilteredListSql_TakesTenNewestRequestsById()
    {
        var sql = OrderRequestRepository.BuildListSql(
            "",
            null,
            applyHeaderJoinsAfterPaging: true,
            latestUnfilteredOnly: true);

        Assert.Contains("SELECT TOP (10)", sql);
        Assert.Contains("FROM dbo.OrderRequests AS R", sql);
        Assert.Contains("ORDER BY R.Id DESC", sql);
        Assert.True(sql.IndexOf("ORDER BY R.Id DESC", StringComparison.Ordinal)
            < sql.IndexOf("OUTER APPLY", StringComparison.Ordinal));
    }

    [Fact]
    public void LatestUnfilteredStatsSql_UsesOnlyTenNewestRequests()
    {
        var sql = OrderRequestRepository.BuildStatsSql(
            "",
            useFilteredBaseRows: false,
            latestUnfilteredOnly: true);

        Assert.Contains("SELECT TOP (10)", sql);
        Assert.Contains("FROM LatestRequests AS R", sql);
        Assert.Contains("ORDER BY R.Id DESC", sql);
        Assert.DoesNotContain("WITH LatestHeaders AS", sql);
    }
}
