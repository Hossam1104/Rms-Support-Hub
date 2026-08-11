using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using RmsSupportHub.Pos.Application.Restore;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Windows;

public sealed partial class SqlCmdExecutor : IDatabaseService, IDatabaseRestoreVerifier, IMaintenanceDatabasePreview, IMaintenanceDatabaseReset
{
    public async Task TestConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString(settings);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand("SELECT 1;", connection) { CommandTimeout = 5 };
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> BranchExistsAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default) =>
        BranchExistsInDatabaseAsync(
            settings,
            DatabaseResolver.ResolveBranchDatabase(settings),
            branchCode,
            cancellationToken);

    public async Task<bool> BranchExistsInDatabaseAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SET NOCOUNT ON;
            SELECT CAST(CASE WHEN EXISTS
            (
                SELECT 1
                FROM Branches
                WHERE BranchCode = @branch_code
            )
            THEN 1 ELSE 0 END AS bit);
            """;

        await using var connection = new SqlConnection(BuildConnectionString(settings, databaseName));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@branch_code", SqlDbType.NVarChar, 50).Value = branchCode;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return ReadBoolScalar(value);
    }

    public async Task ResetBranchDataAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default)
    {
        await ResetBranchDataAsync(
            settings,
            DatabaseResolver.ResolveBranchDatabase(settings),
            branchCode,
            ["Sales", "CashierSessions", "InventoryMovements"],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MaintenanceTableScope>> GetBranchResetScopeAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        IReadOnlyList<string> tableNames,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(BuildConnectionString(settings, databaseName));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var result = new List<MaintenanceTableScope>();
        foreach (var tableName in tableNames)
        {
            var quotedTable = QuoteTableName(tableName);
            var sql = $"SELECT COUNT_BIG(*) FROM {quotedTable} WHERE BranchCode = @branch_code;";
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
            command.Parameters.Add("@branch_code", SqlDbType.NVarChar, 50).Value = branchCode;
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            result.Add(new MaintenanceTableScope(tableName, scalar is null or DBNull ? null : Convert.ToInt64(scalar, System.Globalization.CultureInfo.InvariantCulture)));
        }

        return result;
    }

    public async Task ResetBranchDataAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        IReadOnlyList<string> tableNames,
        CancellationToken cancellationToken = default)
    {
        var deleteStatements = tableNames
            .Select(tableName => $"DELETE FROM {QuoteTableName(tableName)} WHERE BranchCode = @branch_code;")
            .ToArray();
        if (deleteStatements.Length == 0) throw new InvalidOperationException("No reset tables were supplied.");

        var sql = string.Concat(
            "SET NOCOUNT ON;\n",
            "BEGIN TRY\n",
            "    BEGIN TRAN;\n",
            string.Join(Environment.NewLine, deleteStatements.Select(statement => "    " + statement)),
            "\n    COMMIT TRAN;\n",
            "END TRY\n",
            "BEGIN CATCH\n",
            "    IF @@TRANCOUNT > 0 ROLLBACK TRAN;\n",
            "    THROW;\n",
            "END CATCH;");

        await using var connection = new SqlConnection(BuildConnectionString(settings, databaseName));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        command.Parameters.Add("@branch_code", SqlDbType.NVarChar, 50).Value = branchCode;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task BackupDatabaseAsync(AppSettings settings, string databaseName, string backupFilePath, bool useCompatibilityMode, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);

        var quotedDatabase = QuoteDatabaseName(databaseName);
        var options = useCompatibilityMode ? "WITH INIT;" : "WITH INIT, COMPRESSION, CHECKSUM;";
        var sql = string.Concat("BACKUP DATABASE ", quotedDatabase, " TO DISK = @backup_path ", options);

        await using var connection = new SqlConnection(BuildConnectionString(settings, "master"));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        command.Parameters.Add("@backup_path", SqlDbType.NVarChar, 4000).Value = backupFilePath;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RestoreFileInfo>> ReadRestoreFileListAsync(AppSettings settings, string backupFilePath, CancellationToken cancellationToken = default)
    {
        const string sql = "RESTORE FILELISTONLY FROM DISK = @backup_path;";

        await using var connection = new SqlConnection(BuildConnectionString(settings, "master"));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        command.Parameters.Add("@backup_path", SqlDbType.NVarChar, 4000).Value = backupFilePath;

        var files = new List<RestoreFileInfo>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var logicalNameOrdinal = reader.GetOrdinal("LogicalName");
        var typeOrdinal = reader.GetOrdinal("Type");

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var logicalName = reader.GetString(logicalNameOrdinal);
            var fileType = reader.GetString(typeOrdinal);
            if (fileType is "D" or "L")
            {
                files.Add(new RestoreFileInfo(logicalName, fileType));
            }
        }

        return files;
    }

    public async Task RestoreDatabaseAsync(
        AppSettings settings,
        string targetDatabase,
        string backupFilePath,
        IReadOnlyList<RestoreFileInfo> logicalFiles,
        string dbFilesPath,
        CancellationToken cancellationToken = default)
    {
        var quotedDatabase = QuoteDatabaseName(targetDatabase);
        var moveClauses = BuildMoveClauses(targetDatabase, logicalFiles, dbFilesPath);

        var sql = string.Concat(
            "BEGIN TRY\n",
            "    IF DB_ID(@target_database) IS NOT NULL ALTER DATABASE ", quotedDatabase, " SET SINGLE_USER WITH ROLLBACK IMMEDIATE;\n",
            "    RESTORE DATABASE ", quotedDatabase, " FROM DISK = @backup_path WITH REPLACE, RECOVERY",
            moveClauses.Length > 0 ? string.Concat(", ", moveClauses) : string.Empty,
            ";\n",
            "    ALTER DATABASE ", quotedDatabase, " SET MULTI_USER;\n",
            "END TRY\n",
            "BEGIN CATCH\n",
            "    IF DB_ID(@target_database) IS NOT NULL ALTER DATABASE ", quotedDatabase, " SET MULTI_USER;\n",
            "    THROW;\n",
            "END CATCH;");

        await using var connection = new SqlConnection(BuildConnectionString(settings, "master"));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        command.Parameters.Add("@target_database", SqlDbType.NVarChar, 128).Value = targetDatabase;
        command.Parameters.Add("@backup_path", SqlDbType.NVarChar, 4000).Value = backupFilePath;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> VerifyRestoreAsync(
        AppSettings settings,
        string targetDatabase,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT CASE WHEN DB_ID(@target_database) IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END;";
        await using var connection = new SqlConnection(BuildConnectionString(settings, "master"));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@target_database", SqlDbType.NVarChar, 128).Value = targetDatabase;
        return ReadBoolScalar(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static string BuildConnectionString(AppSettings settings, string? database = null)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrWhiteSpace(settings.SqlInstance) ? "." : settings.SqlInstance,
            InitialCatalog = string.IsNullOrWhiteSpace(database)
                ? DatabaseResolver.ResolvePrimaryDatabase(settings)
                : database,
            TrustServerCertificate = true,
            ConnectTimeout = 5
        };

        if (string.IsNullOrWhiteSpace(settings.SqlUser))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = settings.SqlUser;
            builder.Password = settings.SqlPassword;
        }

        return builder.ConnectionString;
    }

    private static string QuoteDatabaseName(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName) || !SafeIdentifierRegex().IsMatch(databaseName))
        {
            throw new InvalidOperationException("Database name contains unsupported characters.");
        }

        using var builder = new SqlCommandBuilder();
        return builder.QuoteIdentifier(databaseName);
    }

    private static string QuoteTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName) || !SafeIdentifierRegex().IsMatch(tableName))
        {
            throw new InvalidOperationException("Table name contains unsupported characters.");
        }

        using var builder = new SqlCommandBuilder();
        return builder.QuoteIdentifier(tableName);
    }

    private static string BuildMoveClauses(string targetDatabase, IReadOnlyList<RestoreFileInfo> logicalFiles, string dbFilesPath)
    {
        var plan = new RestoreSqlPlanBuilder().Build(targetDatabase, dbFilesPath, logicalFiles);
        return string.Join(", ", plan.Moves.Select(move => string.Concat(
                "MOVE N'",
                EscapeSqlLiteral(move.LogicalName),
                "' TO N'",
                EscapeSqlLiteral(move.DestinationPath),
                "'")));
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static bool ReadBoolScalar(object? value)
    {
        return value switch
        {
            bool boolean => boolean,
            byte or sbyte or short or int or long => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) != 0,
            _ => false
        };
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex SafeIdentifierRegex();
}
