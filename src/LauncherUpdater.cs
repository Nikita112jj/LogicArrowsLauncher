using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace LogicArrowsLauncher;

public sealed record UpdateInfo(
    string TagName,
    Version Version,
    string ReleaseName,
    string ReleaseNotes,
    string DownloadUrl,
    long FileSize,
    bool IsPatch = false,
    long ReleaseId = 0,
    DateTimeOffset? PublishedAt = null
);

public static class LauncherUpdater
{
    private const string ApiUrl = "https://api.github.com/repos/Nikita112jj/LogicArrowsLauncher/releases/latest";
    public static readonly Version CurrentVersion =
        Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? new Version(v.Major, v.Minor, Math.Max(0, v.Build))
            : new Version(1, 3, 0);

    public static async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(8);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LogicArrowsLauncher", CurrentVersion.ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

        try
        {
            var response = await client.GetAsync(ApiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagProp)) return null;
            var tag = tagProp.GetString() ?? string.Empty;
            var parsedVer = ParseVersion(tag);
            if (parsedVer is null) return null;

            var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tag : tag;
            var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;
            long releaseId = root.TryGetProperty("id", out var idProp) ? idProp.GetInt64() : 0;

            DateTimeOffset? publishedAt = null;
            if (root.TryGetProperty("published_at", out var pubProp) &&
                DateTimeOffset.TryParse(pubProp.GetString(), out var parsedPub))
            {
                publishedAt = parsedPub;
            }

            string? downloadUrl = null;
            long fileSize = 0;
            DateTimeOffset? assetUpdatedAt = null;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = asset.TryGetProperty("name", out var an) ? an.GetString() : null;
                    if (assetName != null && assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var du) ? du.GetString() : null;
                        fileSize = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                        if (asset.TryGetProperty("updated_at", out var au) &&
                            DateTimeOffset.TryParse(au.GetString(), out var parsedAu))
                        {
                            assetUpdatedAt = parsedAu;
                        }
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl)) return null;

            // Determine if this is a new major/minor version or a mini patch update
            bool isNewerVersion = parsedVer > CurrentVersion;
            bool isPatch = false;

            if (!isNewerVersion)
            {
                // Check if this is a patch on the same or current version
                var exePath = Environment.ProcessPath ?? string.Empty;
                if (File.Exists(exePath))
                {
                    var localExeWriteTime = File.GetLastWriteTimeUtc(exePath);
                    var localExeSize = new FileInfo(exePath).Length;

                    var remoteTime = assetUpdatedAt ?? publishedAt;
                    // If remote asset was published/updated after our local file, or tag/body indicates patch
                    bool newerRemote = remoteTime.HasValue && remoteTime.Value.UtcDateTime > localExeWriteTime.AddMinutes(2);
                    bool sizeDifferent = fileSize > 0 && Math.Abs(fileSize - localExeSize) > 512;
                    bool nameMentionsPatch = name.Contains("патч", StringComparison.OrdinalIgnoreCase) ||
                                            name.Contains("patch", StringComparison.OrdinalIgnoreCase) ||
                                            name.Contains("hotfix", StringComparison.OrdinalIgnoreCase) ||
                                            tag.Contains("patch", StringComparison.OrdinalIgnoreCase);

                    if (newerRemote || (nameMentionsPatch && sizeDifferent))
                    {
                        isPatch = true;
                    }
                }

                if (!isPatch) return null;
            }

            return new UpdateInfo(tag, parsedVer, name, body, downloadUrl, fileSize, isPatch, releaseId, publishedAt);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string> DownloadUpdateAsync(
        UpdateInfo update,
        IProgress<int> progress,
        CancellationToken cancellationToken = default)
    {
        var tempExePath = Path.Combine(Path.GetTempPath(), $"LogicArrowsLauncher_v{update.Version}_{Guid.NewGuid():N}.exe");

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LogicArrowsLauncher", CurrentVersion.ToString()));

        using var response = await client.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? update.FileSize;
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(tempExePath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
            totalRead += read;
            if (totalBytes > 0)
            {
                var percentage = (int)((totalRead * 100) / totalBytes);
                progress.Report(Math.Clamp(percentage, 0, 100));
            }
        }

        return tempExePath;
    }

#if !LINUX_PORT
    /// <summary>Журнал неудачного самообновления: LauncherForm показывает его при следующем старте.</summary>
    public static string FailedUpdateLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LogicArrowsLauncher",
        "update-failed.log");

    public static void ApplyUpdateAndRestart(string tempExePath)
    {
        var currentExe = Environment.ProcessPath ?? Application.ExecutablePath;
        var currentDir = Path.GetDirectoryName(currentExe) ?? ".";
        var currentName = Path.GetFileName(currentExe);
        var backupName = Path.ChangeExtension(currentName, ".old.exe");
        var backupPath = Path.Combine(currentDir, backupName);
        var pid = Process.GetCurrentProcess().Id;
        var updateScript = Path.Combine(Path.GetTempPath(), $"update_logic_arrows_{Guid.NewGuid():N}.cmd");

        // Схема устойчива к антивирусу и второй копии лаунчера:
        // 1) ждём выхода текущего процесса (до 15 сек);
        // 2) ПЕРЕИМЕНОВЫВАЕМ текущий exe в .old — rename разрешён даже для запущенного файла;
        // 3) кладём новый exe на место с ограниченными ретраями (антивирус может держать файл);
        // 4) при неудаче откатываем .old обратно и пишем журнал — при старте покажем сообщение.
        var scriptContent = $@"@echo off
chcp 65001 >nul
timeout /t 1 /nobreak >nul
set /a waits=0
:waitloop
tasklist /fi ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul
if errorlevel 1 goto doreplace
set /a waits+=1
if %waits% geq 15 goto doreplace
timeout /t 1 /nobreak >nul
goto waitloop
:doreplace
if exist ""{backupPath}"" del /f /q ""{backupPath}"" 2>nul
ren ""{currentExe}"" ""{backupName}"" 2>nul
set /a tries=0
:moveloop
if exist ""{currentExe}"" goto replaced
move /y ""{tempExePath}"" ""{currentExe}"" >nul 2>&1
if exist ""{currentExe}"" goto replaced
set /a tries+=1
if %tries% geq 20 goto failed
timeout /t 1 /nobreak >nul
goto moveloop
:replaced
if exist ""{backupPath}"" del /f /q ""{backupPath}"" 2>nul
start """" ""{currentExe}""
del ""%~f0""
exit
:failed
if not exist ""{currentExe}"" if exist ""{backupPath}"" ren ""{backupPath}"" ""{currentName}"" 2>nul
echo %date% %time% > ""{FailedUpdateLogPath}""
start """" ""{currentExe}""
del ""%~f0""
";
        File.WriteAllText(updateScript, scriptContent, System.Text.Encoding.UTF8);

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{updateScript}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
        Application.Exit();
    }

#else
    public static void ApplyUpdateAndRestart(string tempExePath)
    {
        throw new PlatformNotSupportedException("Автообновление на Linux пока не реализовано — скачайте свежий релиз со страницы GitHub Releases.");
    }

#endif
    public static Version? ParseVersion(string tag)
    {
        var clean = tag.TrimStart('v', 'V').Trim();
        var parts = clean.Split(new[] { '.', '-', '+' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        int major = 0, minor = 0, build = 0;
        if (parts.Length > 0 && int.TryParse(parts[0], out var ma)) major = ma;
        if (parts.Length > 1 && int.TryParse(parts[1], out var mi)) minor = mi;
        if (parts.Length > 2 && int.TryParse(parts[2], out var bu)) build = bu;

        return new Version(major, minor, build);
    }
}