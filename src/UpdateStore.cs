using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogicArrowsLauncher;

public sealed class UpdateStore
{
    private const string MetadataFileName = "_cache-manifest.json";
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string RootDirectory { get; }

    private string VersionsDirectory => Path.Combine(RootDirectory, "versions");
    private string ActiveVersionFile => Path.Combine(RootDirectory, "active-version.txt");

    public UpdateStore(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(VersionsDirectory);
    }

    public async Task<AssetSnapshot?> LoadAsync(CancellationToken cancellationToken)
    {
        var versionDirectory = TryGetActiveVersionDirectory();
        if (versionDirectory is null) return null;

        var metadataPath = Path.Combine(versionDirectory, MetadataFileName);
        try
        {
            await using var stream = new FileStream(
                metadataPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                32 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var manifest = await JsonSerializer.DeserializeAsync<CacheManifest>(
                stream,
                jsonOptions,
                cancellationToken).ConfigureAwait(false);
            if (manifest is null ||
                !string.Equals(manifest.Origin, ResourceCatalog.Origin, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(manifest.GameVersion, ResourceCatalog.CurrentVersion, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var assets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var states = new Dictionary<string, CachedAssetState>(StringComparer.OrdinalIgnoreCase);
            foreach (var state in manifest.Assets)
            {
                var localPath = NormalizeLocalPath(state.LocalPath);
                if (localPath is null) return null;
                var path = Path.Combine(versionDirectory, localPath);
                if (!File.Exists(path)) return null;

                var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                if (bytes.LongLength != state.Length ||
                    !string.Equals(
                        Convert.ToHexString(SHA256.HashData(bytes)),
                        state.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                assets[localPath] = bytes;
                states[localPath] = state;
            }

            return new AssetSnapshot(assets, states);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(
        IReadOnlyDictionary<string, byte[]> assets,
        IReadOnlyDictionary<string, CachedAssetState> states,
        CancellationToken cancellationToken)
    {
        var sessionDirectory = Path.Combine(
            VersionsDirectory,
            $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sessionDirectory);
        try
        {
            foreach (var pair in assets)
            {
                var relativePath = NormalizeLocalPath(pair.Key)
                    ?? throw new InvalidDataException($"Недопустимый путь ресурса: {pair.Key}");
                var destination = Path.Combine(sessionDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await File.WriteAllBytesAsync(destination, pair.Value, cancellationToken).ConfigureAwait(false);
            }

            var manifest = new CacheManifest
            {
                Origin = ResourceCatalog.Origin,
                GameVersion = ResourceCatalog.CurrentVersion,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Assets = states.Values
                    .Where(state => assets.ContainsKey(state.LocalPath))
                    .OrderBy(state => state.LocalPath, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            };
            var metadataPath = Path.Combine(sessionDirectory, MetadataFileName);
            await using (var metadataStream = new FileStream(
                metadataPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                32 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(metadataStream, manifest, jsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            await ActivateAsync(sessionDirectory, cancellationToken).ConfigureAwait(false);
            CleanupOldVersions(sessionDirectory);
        }
        catch
        {
            TryDeleteDirectory(sessionDirectory);
            throw;
        }
    }

    private async Task ActivateAsync(string sessionDirectory, CancellationToken cancellationToken)
    {
        var tempPointer = ActiveVersionFile + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPointer, sessionDirectory, cancellationToken).ConfigureAwait(false);
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.Move(tempPointer, ActiveVersionFile, overwrite: true);
                    return;
                }
                catch (IOException) when (attempt < 4)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(75 * (attempt + 1)), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            File.Move(tempPointer, ActiveVersionFile, overwrite: true);
        }
        finally
        {
            TryDeleteFile(tempPointer);
        }
    }

    private string? TryGetActiveVersionDirectory()
    {
        try
        {
            if (!File.Exists(ActiveVersionFile)) return null;
            var marked = File.ReadAllText(ActiveVersionFile).Trim();
            if (string.IsNullOrWhiteSpace(marked)) return null;
            var fullMarked = Path.GetFullPath(marked);
            var fullVersions = Path.GetFullPath(VersionsDirectory) + Path.DirectorySeparatorChar;
            return fullMarked.StartsWith(fullVersions, StringComparison.OrdinalIgnoreCase) &&
                   Directory.Exists(fullMarked)
                ? fullMarked
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void CleanupOldVersions(string activeDirectory)
    {
        if (!Directory.Exists(VersionsDirectory)) return;
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            activeDirectory,
        };
        foreach (var directory in new DirectoryInfo(VersionsDirectory)
                     .GetDirectories()
                     .OrderByDescending(directory => directory.LastWriteTimeUtc)
                     .Take(2))
        {
            keep.Add(directory.FullName);
        }
        foreach (var directory in new DirectoryInfo(VersionsDirectory).GetDirectories())
        {
            if (!keep.Contains(directory.FullName))
            {
                try { directory.Delete(recursive: true); } catch { }
            }
        }
    }

    private static string? NormalizeLocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return null;
        var normalized = path.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine("root", normalized));
        var root = Path.GetFullPath("root") + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
        return normalized.TrimStart(Path.DirectorySeparatorChar);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // A failed cleanup does not affect the next unique version.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
