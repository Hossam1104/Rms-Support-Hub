using Microsoft.Data.SqlClient;
using RmsSupportHub.Pos.Application.Restore;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Databases;

/// <summary>
/// Native SQL Server adapter for the typed RMS database workflow. The installed RMS connection
/// string is read through the dedicated secret-bearing discovery seam and is never returned,
/// logged, or placed in an operation result. Database names, SQL statements, and SQL file moves
/// are all server-owned.
/// </summary>
public sealed class RmsSqlDatabaseOperations(
    IRmsDatabaseConnectionStringSource connectionStrings,
    IRestoreSqlPlanBuilder sqlPlanBuilder) : IRmsDatabaseSqlOperations
{
    public async Task<RmsDatabaseSqlBackupResult> BackupAsync(
        RmsDatabaseKind database,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        var definition = RmsDatabaseCatalog.For(database);
        await using var identityConnection = await OpenConnectionAsync(database, useMaster: false, cancellationToken)
            .ConfigureAwait(false);
        if (identityConnection is null)
        {
            return Failed("backup_connection_unavailable", "The installed RMS database connection could not be opened.");
        }

        if (!await HasExpectedIdentityAsync(identityConnection, definition.DatabaseName, cancellationToken).ConfigureAwait(false))
        {
            return Failed("backup_database_identity_mismatch", "The SQL connection did not resolve to the canonical RMS database.");
        }

        await using var masterConnection = await OpenConnectionAsync(database, useMaster: true, cancellationToken)
            .ConfigureAwait(false);
        if (masterConnection is null)
        {
            return Failed("backup_master_connection_unavailable", "The SQL Server master connection could not be opened.");
        }

        var quotedDatabase = QuoteIdentifier(definition.DatabaseName);
        await using var command = masterConnection.CreateCommand();
        command.CommandText = $"BACKUP DATABASE {quotedDatabase} TO DISK = @backup_path WITH INIT, COMPRESSION, CHECKSUM;";
        command.CommandTimeout = 0;
        command.Parameters.Add("@backup_path", System.Data.SqlDbType.NVarChar, 4000).Value = backupPath;

        var dispatchStarted = false;
        try
        {
            dispatchStarted = true;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new(RmsDatabaseSqlOutcome.Completed, "backup_sql_completed", "The SQL Server backup command completed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return dispatchStarted
                ? Unknown("backup_sql_outcome_unknown", "The SQL Server backup dispatch was interrupted; its outcome is unknown.")
                : Failed("backup_sql_not_started", "The SQL Server backup was not started.");
        }
        catch
        {
            return dispatchStarted
                ? Unknown("backup_sql_outcome_unknown", "The SQL Server backup dispatch failed ambiguously; its outcome is unknown.")
                : Failed("backup_sql_failed", "The SQL Server backup was rejected before dispatch.");
        }
    }

    public async Task<RmsDatabaseSqlInspectionResult> InspectBackupAsync(
        RmsDatabaseKind database,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        var definition = RmsDatabaseCatalog.For(database);

        // Restore must remain possible when the target database itself cannot open, so inspection
        // only requires the SQL Server master connection, never the target database connection.
        await using var connection = await OpenConnectionAsync(database, useMaster: true, cancellationToken)
            .ConfigureAwait(false);
        if (connection is null)
        {
            return InspectionFailed("restore_master_connection_unavailable", "The SQL Server master connection could not be opened.");
        }

        try
        {
            await using (var header = connection.CreateCommand())
            {
                header.CommandText = "RESTORE HEADERONLY FROM DISK = @backup_path;";
                header.CommandTimeout = 120;
                header.Parameters.Add("@backup_path", System.Data.SqlDbType.NVarChar, 4000).Value = backupPath;
                await using var headerReader = await header.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var headerDatabaseNameOrdinal = headerReader.GetOrdinal("DatabaseName");
                string? headerDatabaseName = null;
                if (await headerReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    headerDatabaseName = headerReader.IsDBNull(headerDatabaseNameOrdinal)
                        ? null
                        : headerReader.GetString(headerDatabaseNameOrdinal);
                }

                if (!string.Equals(headerDatabaseName, definition.DatabaseName, StringComparison.OrdinalIgnoreCase))
                {
                    return InspectionFailed("restore_backup_database_mismatch", "The approved backup does not belong to the selected canonical database.");
                }
            }

            await using (var verify = connection.CreateCommand())
            {
                verify.CommandText = "RESTORE VERIFYONLY FROM DISK = @backup_path;";
                verify.CommandTimeout = 120;
                verify.Parameters.Add("@backup_path", System.Data.SqlDbType.NVarChar, 4000).Value = backupPath;
                await verify.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var fileList = connection.CreateCommand();
            fileList.CommandText = "RESTORE FILELISTONLY FROM DISK = @backup_path;";
            fileList.CommandTimeout = 120;
            fileList.Parameters.Add("@backup_path", System.Data.SqlDbType.NVarChar, 4000).Value = backupPath;
            var files = new List<RestoreFileInfo>();
            await using var reader = await fileList.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var logicalNameOrdinal = reader.GetOrdinal("LogicalName");
            var typeOrdinal = reader.GetOrdinal("Type");
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var logicalName = reader.IsDBNull(logicalNameOrdinal) ? string.Empty : reader.GetString(logicalNameOrdinal);
                var fileType = reader.IsDBNull(typeOrdinal) ? string.Empty : reader.GetString(typeOrdinal);
                if (fileType is "D" or "L")
                {
                    files.Add(new RestoreFileInfo(logicalName, fileType));
                }
            }

            return files.Count == 0
                ? InspectionFailed("restore_sql_file_list_invalid", "The approved backup did not provide a usable SQL file list.")
                : new(RmsDatabaseSqlOutcome.Completed, "restore_sql_inspection_completed", "The approved SQL backup passed bounded inspection.", files);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return InspectionFailed("restore_sql_inspection_failed", "The approved SQL backup could not be inspected.");
        }
    }

    public async Task<RmsDatabaseSqlRestoreResult> RestoreAsync(
        RmsDatabaseKind database,
        string backupPath,
        IReadOnlyList<RestoreFileInfo> logicalFiles,
        string databaseFilesRoot,
        CancellationToken cancellationToken = default)
    {
        var definition = RmsDatabaseCatalog.For(database);
        RestoreSqlPlan plan;
        try
        {
            plan = sqlPlanBuilder.Build(definition.DatabaseName, databaseFilesRoot, logicalFiles);
        }
        catch (RestorePolicyException exception)
        {
            return new(RmsDatabaseSqlOutcome.NotAttempted, exception.Code, exception.SafeMessage, false, []);
        }
        catch
        {
            return new(RmsDatabaseSqlOutcome.NotAttempted, "restore_sql_plan_invalid", "The server-owned SQL restore plan is invalid.", false, []);
        }

        await using var connection = await OpenConnectionAsync(database, useMaster: true, cancellationToken)
            .ConfigureAwait(false);
        if (connection is null)
        {
            return new(RmsDatabaseSqlOutcome.NotAttempted, "restore_master_connection_unavailable", "The SQL Server master connection could not be opened.", false, []);
        }

        var quotedDatabase = QuoteIdentifier(definition.DatabaseName);
        var moves = string.Join(", ", plan.Moves.Select(move =>
            $"MOVE N'{EscapeSqlLiteral(move.LogicalName)}' TO N'{EscapeSqlLiteral(move.DestinationPath)}'"));
        var sql = $"""
            SET NOCOUNT ON;
            BEGIN TRY
                IF DB_ID(@target_database) IS NOT NULL ALTER DATABASE {quotedDatabase} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                RESTORE DATABASE {quotedDatabase} FROM DISK = @backup_path WITH REPLACE, RECOVERY, {moves};
                ALTER DATABASE {quotedDatabase} SET MULTI_USER;
            END TRY
            BEGIN CATCH
                BEGIN TRY
                    IF DB_ID(@target_database) IS NOT NULL ALTER DATABASE {quotedDatabase} SET MULTI_USER;
                END TRY
                BEGIN CATCH
                END CATCH;
                THROW;
            END CATCH;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 0;
        command.Parameters.Add("@target_database", System.Data.SqlDbType.NVarChar, 128).Value = definition.DatabaseName;
        command.Parameters.Add("@backup_path", System.Data.SqlDbType.NVarChar, 4000).Value = backupPath;

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new(RmsDatabaseSqlOutcome.Completed, "restore_sql_completed", "The SQL Server restore command completed.", false, []);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(
                RmsDatabaseSqlOutcome.OutcomeUnknown,
                "restore_sql_outcome_unknown",
                "The SQL Server restore dispatch was interrupted; its outcome is unknown.",
                true,
                ["Database access mode recovery was attempted by the server-owned SQL plan."]);
        }
        catch
        {
            return new(
                RmsDatabaseSqlOutcome.OutcomeUnknown,
                "restore_sql_outcome_unknown",
                "The SQL Server restore dispatch failed ambiguously; its outcome is unknown.",
                true,
                ["Database access mode recovery was attempted by the server-owned SQL plan."]);
        }
    }

    public async Task<bool> VerifyDatabaseAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default)
    {
        var definition = RmsDatabaseCatalog.For(database);
        await using var connection = await OpenConnectionAsync(database, useMaster: false, cancellationToken)
            .ConfigureAwait(false);
        return connection is not null
            && await HasExpectedIdentityAsync(connection, definition.DatabaseName, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqlConnection?> OpenConnectionAsync(
        RmsDatabaseKind database,
        bool useMaster,
        CancellationToken cancellationToken)
    {
        try
        {
            var raw = await connectionStrings.GetConnectionStringAsync(database, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var builder = new SqlConnectionStringBuilder(raw)
            {
                ConnectTimeout = 5
            };
            if (useMaster)
            {
                builder.InitialCatalog = "master";
            }

            var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> HasExpectedIdentityAsync(
        SqlConnection connection,
        string expectedDatabase,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT DB_NAME();";
            command.CommandTimeout = 5;
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return string.Equals(value as string, expectedDatabase, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static string QuoteIdentifier(string value)
    {
        using var builder = new SqlCommandBuilder();
        return builder.QuoteIdentifier(value);
    }

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static RmsDatabaseSqlBackupResult Failed(string code, string detail) =>
        new(RmsDatabaseSqlOutcome.Failed, code, detail);

    private static RmsDatabaseSqlBackupResult Unknown(string code, string detail) =>
        new(RmsDatabaseSqlOutcome.OutcomeUnknown, code, detail);

    private static RmsDatabaseSqlInspectionResult InspectionFailed(string code, string detail) =>
        new(RmsDatabaseSqlOutcome.Failed, code, detail, []);
}
