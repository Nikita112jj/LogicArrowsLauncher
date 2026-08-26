using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace LogicArrowsLauncher;

public sealed class AssetSynchronizer : IDisposable
{
    private const long MaxAssetBytes = 32L * 1024 * 1024;

    private readonly HttpClient client;
    private readonly UpdateStore updateStore;
    private readonly Dictionary<string, string> localPathByRemotePath;
    private readonly ConcurrentDictionary<string, byte[]> memoryAssets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CachedAssetState> memoryStates = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    public AssetSynchronizer(UpdateStore updateStore)
    {
        this.updateStore = updateStore;
        localPathByRemotePath = ResourceCatalog.All
            .GroupBy(asset => NormalizeRemotePath(asset.RemotePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().LocalPath, StringComparer.OrdinalIgnoreCase);

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = false,
            UseCookies = false,
        };
        client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LogicArrowsLauncher/4.0");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
    }

    public bool HasRequiredCache()
    {
        return new[] { "index.html", "bundle-shell.js", "bundle.js", "style.css", "manifest.json" }
            .All(memoryAssets.ContainsKey);
    }

    public bool TryGetAsset(string requestPath, out byte[] content, out string relativePath)
    {
        var normalized = NormalizeRemotePath(requestPath);
        if (normalized == "/") normalized = "/index.html";
        if (!localPathByRemotePath.TryGetValue(normalized, out relativePath!))
        {
            content = Array.Empty<byte>();
            relativePath = "";
            return false;
        }
        return memoryAssets.TryGetValue(relativePath, out content!);
    }

    public async Task<SyncSummary> SyncAsync(IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        memoryAssets.Clear();
        memoryStates.Clear();

        var cached = await updateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            foreach (var pair in cached.Assets)
            {
                memoryAssets[pair.Key] = pair.Value;
            }
            foreach (var pair in cached.States)
            {
                memoryStates[pair.Key] = pair.Value;
            }

            var fastCheck = await TryFastVersionCheckAsync(progress, cancellationToken).ConfigureAwait(false);
            if (fastCheck.CanUseSnapshot)
            {
                progress?.Report(new SyncProgress(
                    ResourceCatalog.VersionSentinels.Count,
                    ResourceCatalog.VersionSentinels.Count,
                    "Сохранённая версия",
                    "Версия не изменилась",
                    false));
                return new SyncSummary(
                    ResourceCatalog.VersionSentinels.Count,
                    0,
                    ResourceCatalog.VersionSentinels.Count,
                    0,
                    true,
                    true,
                    "Версия не изменилась — использую сохранённую копию.");
            }

            return await SyncAllAsync(
                cached,
                fastCheck.Downloaded,
                progress,
                cancellationToken).ConfigureAwait(false);
        }

        return await SyncAllAsync(null, 0, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<FastCheckResult> TryFastVersionCheckAsync(
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!HasRequiredCache())
        {
            return new FastCheckResult(false, 0);
        }

        var canUseSnapshot = true;
        var downloaded = 0;
        var completed = 0;
        var total = ResourceCatalog.VersionSentinels.Count;
        progress?.Report(new SyncProgress(0, total, "Ключевые файлы", "Быстро проверяю версию", false));

        await Parallel.ForEachAsync(
            ResourceCatalog.VersionSentinels,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = total,
                CancellationToken = cancellationToken,
            },
            async (asset, ct) =>
            {
                try
                {
                    if (!memoryStates.TryGetValue(asset.LocalPath, out var oldState) ||
                        !memoryAssets.ContainsKey(asset.LocalPath))
                    {
                        Volatile.Write(ref canUseSnapshot, false);
                        return;
                    }

                    var result = await DownloadOrReuseAsync(asset, oldState, ct).ConfigureAwait(false);
                    if (!result.NotModified)
                    {
                        memoryAssets[asset.LocalPath] = result.Bytes;
                        memoryStates[asset.LocalPath] = result.State;
                        Interlocked.Increment(ref downloaded);
                        Volatile.Write(ref canUseSnapshot, false);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or IOException or InvalidDataException or TaskCanceledException)
                {
                    Volatile.Write(ref canUseSnapshot, false);
                }
                finally
                {
                    var finished = Interlocked.Increment(ref completed);
                    progress?.Report(new SyncProgress(
                        finished,
                        total,
                        asset.LocalPath,
                        "Быстро проверяю версию",
                        false));
                }
            }).ConfigureAwait(false);

        return new FastCheckResult(canUseSnapshot, downloaded);
    }

    private async Task<SyncSummary> SyncAllAsync(
        AssetSnapshot? cached,
        int alreadyDownloaded,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        var failures = new ConcurrentBag<string>();
        var downloaded = alreadyDownloaded;
        var notModified = 0;
        var failed = 0;
        var checkedCount = 0;
        var finishedCount = 0;
        var totalCount = ResourceCatalog.All.Count;
        var maxParallel = Math.Clamp(Environment.ProcessorCount, 2, 6);

        await Parallel.ForEachAsync(
            ResourceCatalog.All,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallel,
                CancellationToken = cancellationToken,
            },
            async (asset, ct) =>
            {
                Interlocked.Increment(ref checkedCount);
                progress?.Report(new SyncProgress(
                    Volatile.Read(ref finishedCount),
                    totalCount,
                    asset.LocalPath,
                    cached is null ? "Скачивается" : "Проверяется обновление",
                    false));

                memoryStates.TryGetValue(asset.LocalPath, out var oldState);
                try
                {
                    var result = await DownloadOrReuseAsync(asset, oldState, ct).ConfigureAwait(false);
                    memoryAssets[asset.LocalPath] = result.Bytes;
                    memoryStates[asset.LocalPath] = result.State;
                    if (result.NotModified)
                    {
                        Interlocked.Increment(ref notModified);
                    }
                    else
                    {
                        Interlocked.Increment(ref downloaded);
                    }
                    var finished = Interlocked.Increment(ref finishedCount);
                    progress?.Report(new SyncProgress(
                        finished,
                        totalCount,
                        asset.LocalPath,
                        result.NotModified ? "Без изменений" : "Скачано",
                        false));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or IOException or InvalidDataException or TaskCanceledException)
                {
                    if (memoryAssets.ContainsKey(asset.LocalPath) && memoryStates.ContainsKey(asset.LocalPath))
                    {
                        Interlocked.Increment(ref notModified);
                        var finished = Interlocked.Increment(ref finishedCount);
                        progress?.Report(new SyncProgress(
                            finished,
                            totalCount,
                            asset.LocalPath,
                            "Оставлена сохранённая версия",
                            false));
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                        failures.Add($"{asset.LocalPath}: {exception.Message}");
                        var finished = Interlocked.Increment(ref finishedCount);
                        progress?.Report(new SyncProgress(
                            finished,
                            totalCount,
                            asset.LocalPath,
                            "Ошибка",
                            true));
                    }
                }
            }).ConfigureAwait(false);

        if (!HasRequiredCache())
        {
            var details = failures.IsEmpty
                ? "Проверь подключение к интернету и доступ к logic-arrows.io."
                : string.Join(" | ", failures.Take(5));
            throw new InvalidDataException(
                "Не удалось получить обязательные ресурсы Logic Arrows. " + details);
        }

        if (cached is null || downloaded > 0)
        {
            try
            {
                await updateStore.SaveAsync(memoryAssets, memoryStates, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                failures.Add($"Не удалось сохранить версию в updates: {exception.Message}");
            }
        }

        var usedExistingCache = cached is not null && notModified > 0;
        var message = failures.IsEmpty
            ? $"Готово: скачано {downloaded}, без изменений {notModified}."
            : $"Готово с сохранёнными файлами: скачано {downloaded}, без изменений {notModified}, проблем {failures.Count}.";
        return new SyncSummary(checkedCount, downloaded, notModified, failed, usedExistingCache, false, message);
    }

    private async Task<(CachedAssetState State, byte[] Bytes, bool NotModified)> DownloadOrReuseAsync(
        RemoteAsset asset,
        CachedAssetState? oldState,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(new Uri(ResourceCatalog.Origin), asset.RemotePath));
        if (oldState is not null)
        {
            if (EntityTagHeaderValue.TryParse(oldState.ETag, out var etag))
            {
                request.Headers.IfNoneMatch.Add(etag);
            }
            if (DateTimeOffset.TryParse(oldState.LastModified, out var modified))
            {
                request.Headers.IfModifiedSince = modified;
            }
        }

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotModified &&
            memoryAssets.TryGetValue(asset.LocalPath, out var existing) &&
            oldState is not null)
        {
            return (new CachedAssetState
            {
                RemotePath = oldState.RemotePath,
                LocalPath = oldState.LocalPath,
                ETag = oldState.ETag,
                LastModified = oldState.LastModified,
                Sha256 = oldState.Sha256,
                Length = oldState.Length,
                CheckedAtUtc = DateTimeOffset.UtcNow,
            }, existing, true);
        }

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxAssetBytes)
        {
            throw new InvalidDataException($"Файл больше лимита {MaxAssetBytes} байт.");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.LongLength > MaxAssetBytes)
        {
            throw new InvalidDataException($"Файл больше лимита {MaxAssetBytes} байт.");
        }

        return (new CachedAssetState
        {
            RemotePath = asset.RemotePath,
            LocalPath = asset.LocalPath,
            ETag = response.Headers.ETag?.Tag,
            LastModified = response.Content.Headers.LastModified?.ToString("R"),
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Length = bytes.LongLength,
            CheckedAtUtc = DateTimeOffset.UtcNow,
        }, bytes, false);
    }

    private static string NormalizeRemotePath(string path)
    {
        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        var normalized = queryIndex >= 0 ? path[..queryIndex] : path;
        return normalized == "/" ? "/" : "/" + normalized.TrimStart('/');
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        client.Dispose();
        memoryAssets.Clear();
        memoryStates.Clear();
    }

    private sealed record FastCheckResult(bool CanUseSnapshot, int Downloaded);
}
