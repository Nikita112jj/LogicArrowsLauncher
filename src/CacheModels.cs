namespace LogicArrowsLauncher;

public sealed class CachedAssetState
{
    public string RemotePath { get; set; } = "";
    public string LocalPath { get; set; } = "";
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
    public string Sha256 { get; set; } = "";
    public long Length { get; set; }
    public DateTimeOffset CheckedAtUtc { get; set; }
}

public sealed class CacheManifest
{
    public string Origin { get; set; } = ResourceCatalog.Origin;
    public string GameVersion { get; set; } = ResourceCatalog.CurrentVersion;
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public List<CachedAssetState> Assets { get; set; } = new();
}

public sealed record AssetSnapshot(
    IReadOnlyDictionary<string, byte[]> Assets,
    IReadOnlyDictionary<string, CachedAssetState> States);

public sealed record SyncProgress(
    int Completed,
    int Total,
    string AssetPath,
    string Status,
    bool IsError);

public sealed record SyncSummary(
    int Checked,
    int Downloaded,
    int NotModified,
    int Failed,
    bool UsedExistingCache,
    bool FastVersionChecked,
    string Message);
