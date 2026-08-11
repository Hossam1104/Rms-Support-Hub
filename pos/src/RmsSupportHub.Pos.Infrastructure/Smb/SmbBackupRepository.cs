using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Domain.Exceptions;

namespace RmsSupportHub.Pos.Infrastructure.Smb;

/// <summary>
/// Reads the configured server-local backup root over an authenticated administrative drive share.
/// Paths returned to the application remain internal adapter values and are never copied to Agent
/// contracts or operation messages.
/// </summary>
public sealed class SmbBackupRepository : IBackupRepository
{
    public Task<IReadOnlyList<RemoteEntryInfo>> ListDirectoriesAsync(
        RemoteConnectionInfo connection,
        string rootFolder,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    var approvedRoot = SmbPathResolver.ValidateCanonicalRoot(connection.ApprovedRootFolder ?? rootFolder);
                    var requestedRoot = SmbPathResolver.ValidateDrivePath(rootFolder);
                    if (!SmbPathResolver.IsWithinRoot(requestedRoot, approvedRoot))
                    {
                        throw new SmbPathPolicyException("downloader.smb_root_rejected");
                    }

                    var uncRoot = SmbPathResolver.ToUncPath(connection.ServerIp, requestedRoot, approvedRoot);
                    using var scope = SmbConnectionScope.Connect(
                        SmbPathResolver.ToUncShareRoot(connection.ServerIp, approvedRoot),
                        connection.Username,
                        connection.Password);

                    if (!Directory.Exists(uncRoot))
                    {
                        return (IReadOnlyList<RemoteEntryInfo>)[];
                    }

                    var entries = Directory.GetDirectories(uncRoot)
                        .Select(path => ToEntryInfo(requestedRoot, uncRoot, path, isDirectory: true))
                        .ToList();
                    return (IReadOnlyList<RemoteEntryInfo>)entries;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw TranslateRepositoryFailure(exception);
                }
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<RemoteEntryInfo>> ListFilesAsync(
        RemoteConnectionInfo connection,
        string folder,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    var approvedRoot = SmbPathResolver.ValidateCanonicalRoot(connection.ApprovedRootFolder ?? folder);
                    var requestedFolder = SmbPathResolver.ValidateDrivePath(folder);
                    if (!SmbPathResolver.IsWithinRoot(requestedFolder, approvedRoot))
                    {
                        throw new SmbPathPolicyException("downloader.smb_root_rejected");
                    }

                    var uncFolder = SmbPathResolver.ToUncPath(connection.ServerIp, requestedFolder, approvedRoot);
                    using var scope = SmbConnectionScope.Connect(
                        SmbPathResolver.ToUncShareRoot(connection.ServerIp, approvedRoot),
                        connection.Username,
                        connection.Password);

                    if (!Directory.Exists(uncFolder))
                    {
                        return (IReadOnlyList<RemoteEntryInfo>)[];
                    }

                    var entries = Directory.GetFiles(uncFolder)
                        .Select(path => ToEntryInfo(requestedFolder, uncFolder, path, isDirectory: false))
                        .ToList();
                    return (IReadOnlyList<RemoteEntryInfo>)entries;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw TranslateRepositoryFailure(exception);
                }
            },
            cancellationToken);
    }

    public async Task DownloadFileAsync(
        RemoteConnectionInfo connection,
        string remoteFilePath,
        string localFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? partialPath = null;
        try
        {
            var approvedRoot = SmbPathResolver.ValidateCanonicalRoot(connection.ApprovedRootFolder ?? Path.GetPathRoot(remoteFilePath) ?? remoteFilePath);
            var validatedRemoteFile = SmbPathResolver.ValidateRemoteFilePath(remoteFilePath, approvedRoot);
            var uncFile = SmbPathResolver.ToUncPath(connection.ServerIp, validatedRemoteFile, approvedRoot);
            using var scope = SmbConnectionScope.Connect(
                SmbPathResolver.ToUncShareRoot(connection.ServerIp, approvedRoot),
                connection.Username,
                connection.Password);

            var fullLocalPath = ValidateLocalDestination(localFilePath);
            partialPath = fullLocalPath + ".partial";
            TryDeletePartial(partialPath);

            try
            {
                await using (var source = new FileStream(
                    uncFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    options: FileOptions.SequentialScan | FileOptions.Asynchronous))
                await using (var destination = new FileStream(
                    partialPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    options: FileOptions.SequentialScan | FileOptions.Asynchronous))
                {
                    var totalBytes = source.Length;
                    var buffer = new byte[81920];
                    long copied = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        copied += read;
                        if (totalBytes > 0)
                        {
                            progress?.Report((double)copied / totalBytes);
                        }
                    }
                }

                if (File.Exists(fullLocalPath))
                {
                    throw new IOException("The destination already contains a published archive.");
                }

                File.Move(partialPath, fullLocalPath, overwrite: false);
            }
            catch (OperationCanceledException)
            {
                TryDeletePartial(partialPath);
                throw;
            }
            catch
            {
                TryDeletePartial(partialPath);
                throw new BackupRepositoryException(DownloaderFailureCodes.DownloadFailed);
            }
        }
        catch (OperationCanceledException)
        {
            if (partialPath is not null) TryDeletePartial(partialPath);
            throw;
        }
        catch (BackupRepositoryException)
        {
            if (partialPath is not null) TryDeletePartial(partialPath);
            throw;
        }
        catch (Exception exception)
        {
            if (partialPath is not null) TryDeletePartial(partialPath);
            throw TranslateRepositoryFailure(exception);
        }
    }

    private static BackupRepositoryException TranslateRepositoryFailure(Exception exception) => exception switch
    {
        BackupRepositoryException repositoryException => repositoryException,
        SmbPathPolicyException pathException => new BackupRepositoryException(pathException.Code),
        SmbConnectionException connectionException => new BackupRepositoryException(connectionException.Code),
        _ => new BackupRepositoryException(DownloaderFailureCodes.SmbRepositoryFailed),
    };

    private static RemoteEntryInfo ToEntryInfo(string logicalParent, string uncParent, string uncPath, bool isDirectory)
    {
        var name = Path.GetFileName(uncPath.TrimEnd('\\'));
        SmbPathResolver.ValidateSafeFileName(name);
        var logicalPath = Path.Combine(logicalParent, name);
        var createdAtUtc = isDirectory
            ? Directory.GetCreationTimeUtc(uncPath)
            : File.GetCreationTimeUtc(uncPath);
        var sizeBytes = isDirectory ? 0 : new FileInfo(uncPath).Length;

        return new RemoteEntryInfo(name, logicalPath, new DateTimeOffset(createdAtUtc, TimeSpan.Zero), sizeBytes);
    }

    private static string ValidateLocalDestination(string localFilePath)
    {
        if (string.IsNullOrWhiteSpace(localFilePath))
        {
            throw new IOException("The local archive destination is invalid.");
        }

        var fullPath = Path.GetFullPath(localFilePath);
        var directory = Path.GetDirectoryName(fullPath);
        var name = Path.GetFileName(fullPath);
        if (directory is null || !DownloaderInputPolicy.IsSafeArchiveFileName(name))
        {
            throw new IOException("The local archive destination is invalid.");
        }

        Directory.CreateDirectory(directory);
        return fullPath;
    }

    private static void TryDeletePartial(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // A failed cleanup never publishes the partial path as an artifact or returns it to a client.
        }
    }
}
