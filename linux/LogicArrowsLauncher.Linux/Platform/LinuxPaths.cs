namespace LogicArrowsLauncher.Linux.Platform;

/// <summary>
/// Каталоги данных для Linux: XDG-совместимые пути вместо %LocalAppData%.
/// Структура внутри DataRoot повторяет Windows-версию (updates/&lt;версия&gt;, profile).
/// </summary>
public static class LinuxPaths
{
    public static string DataRoot { get; } = ResolveDataRoot();

    /// <summary>Корень кэша обновлений: аналог %LocalAppData%\LogicArrowsLauncher\updates.</summary>
    public static string UpdatesRoot => Path.Combine(DataRoot, "updates");

    /// <summary>Каталог снапшота ресурсов игры текущей версии.</summary>
    public static string UpdatesDirectory => Path.Combine(UpdatesRoot, ResourceCatalog.CurrentVersion);

    /// <summary>Кэш браузерного движка (IndexedDB карт, localStorage, cookies).</summary>
    public static string ProfileDirectory => Path.Combine(DataRoot, "profile");

    /// <summary>Кэш самого CEF (RootCachePath).</summary>
    public static string CefCacheDirectory => Path.Combine(DataRoot, "cef");

    /// <summary>Каталог экспортированных карт (порт кнопки «Открыть папку карт»).</summary>
    public static string MapsDirectory => Path.Combine(DataRoot, "maps");

    private static string ResolveDataRoot()
    {
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathRooted(xdgDataHome))
            return Path.Combine(xdgDataHome, "LogicArrowsLauncher");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "LogicArrowsLauncher");
    }
}
