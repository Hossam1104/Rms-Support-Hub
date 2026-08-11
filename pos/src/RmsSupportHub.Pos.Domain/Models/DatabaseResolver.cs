namespace RmsSupportHub.Pos.Domain.Models;

public static class DatabaseResolver
{
    public static string ResolveBranchDatabase(AppSettings settings)
    {
        var databases = settings.Databases ?? [];
        var branch = databases.FirstOrDefault(db => db.Contains("branch", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(branch))
        {
            return branch;
        }

        var exact = databases.FirstOrDefault(db => string.Equals(db, "RmsBranchSrv", StringComparison.OrdinalIgnoreCase));
        return exact ?? "RmsBranchSrv";
    }

    public static string ResolveCashierDatabase(AppSettings settings)
    {
        var databases = settings.Databases ?? [];
        var cashier = databases.FirstOrDefault(db => db.Contains("cashier", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(cashier))
        {
            return cashier;
        }

        var exact = databases.FirstOrDefault(db => string.Equals(db, "RmsCashierSrv", StringComparison.OrdinalIgnoreCase));
        return exact ?? "RmsCashierSrv";
    }

    public static string ResolvePrimaryDatabase(AppSettings settings)
    {
        return ResolveBranchDatabase(settings);
    }
}
