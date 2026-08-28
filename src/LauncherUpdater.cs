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
    long FileSize
);

public static class LauncherUpdater
{
    private const string ApiUrl = "https://api.github.com/repos/Nikita112jj/LogicArrowsLauncher/releases/latest";
    public static readonly Version CurrentVersion =
        Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? new Version(v.Major, v.Minor, Math.Max(0, v.Build))
            : new Version(1, 2, 0);

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
            if (parsedVer is null || parsedVer <= CurrentVersion) return null;

            var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tag : tag;
            var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;

            string? downloadUrl = null;
            long fileSize = 0;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = asset.TryGetProperty("name", out var an) ? an.GetString() : null;
                    if (assetName != null && assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var du) ? du.GetString() : null;
                        fileSize = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl)) return null;

            return new UpdateInfo(tag, parsedVer, name, body, downloadUrl, fileSize);
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

    public static void ApplyUpdateAndRestart(string tempExePath)
    {
        var currentExe = Application.ExecutablePath;
        var pid = Process.GetCurrentProcess().Id;
        var updateScript = Path.Combine(Path.GetTempPath(), $"update_logic_arrows_{Guid.NewGuid():N}.cmd");

        var scriptContent = $@"@echo off
chcp 65001 >nul
timeout /t 1 /nobreak >nul
:waitloop
tasklist /fi ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)
:retrymove
move /y ""{tempExePath}"" ""{currentExe}"" >nul 2>&1
if errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto retrymove
)
start """" ""{currentExe}""
del ""%~f0""
";
        File.WriteAllText(updateScript, scriptContent, System.Text.Encoding.UTF8);

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{updateScript}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(psi);
        Application.Exit();
    }

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