namespace LogicArrowsLauncher;

public sealed record RemoteAsset(string RemotePath, string LocalPath);

public static class ResourceCatalog
{
    public const string Origin = "https://logic-arrows.io";
    public const string CurrentVersion = "1_4";

    public static IReadOnlyList<RemoteAsset> All { get; } = Build();

    public static IReadOnlyList<RemoteAsset> VersionSentinels { get; } = new[]
    {
        new RemoteAsset("/index.html", "index.html"),
        new RemoteAsset("/bundle-shell.js?v=2026_03_25_4", "bundle-shell.js"),
        new RemoteAsset("/bundle.js?v=1_4", "bundle.js"),
        new RemoteAsset("/style.css?v=1_4", "style.css"),
        new RemoteAsset("/manifest.json", "manifest.json"),
    };

    private static IReadOnlyList<RemoteAsset> Build()
    {
        var assets = new List<RemoteAsset>
        {
            new("/index.html", "index.html"),
            new("/bundle-shell.js?v=2026_03_25_4", "bundle-shell.js"),
            new("/bundle.js?v=1_4", "bundle.js"),
            new("/bundle-1_2_1.js?v=1_2_1", "bundle-1_2_1.js"),
            new("/bundle-1_3.js?v=1_3", "bundle-1_3.js"),
            new("/style.css?v=1_4", "style.css"),
            new("/style-v1_2.css?v=1_2_1", "style-v1_2.css"),
            new("/style-1_3.css?v=1_3", "style-1_3.css"),
            new("/manifest.json", "manifest.json"),
            new("/doc/privacy.html", "doc/privacy.html"),
            new("/doc/terms.html", "doc/terms.html"),
            new("/res/favicon.png", "res/favicon.png"),
            new("/res/favicon512.png", "res/favicon512.png"),
            new("/res/fonts/Nunito-VariableFont_wght.ttf", "res/fonts/Nunito-VariableFont_wght.ttf"),
            new("/res/fonts/Roboto-Regular.ttf", "res/fonts/Roboto-Regular.ttf"),
        };

        var icons = new[]
        {
            "icon-google.svg", "icon-guide.svg", "icon-levels.svg", "icon-maps.svg",
            "icon-news.svg", "icon-public.svg", "icon-settings.svg", "icon-user.svg",
            "icon_bin.svg", "icon_discord.svg", "icon_google.svg", "icon_like.svg",
            "icon_like_active.svg", "icon_mouse.svg", "icon_mouse_left.svg",
            "icon_mouse_middle.svg", "icon_public.svg", "icon_public_active.svg",
            "icon_telegram.svg", "icon_undo.svg", "icon_youtube.svg", "level-play-button.svg",
            "menu-back-icon.svg", "toolbar-arrow.svg",
        };
        assets.AddRange(icons.Select(name =>
            new RemoteAsset($"/res/icons/{name}", $"res/icons/{name}")));

        assets.Add(new RemoteAsset("/res/sprites/atlas.png?v=1_4", "res/sprites/atlas.png"));
        for (var i = 1; i <= 26; i++)
        {
            assets.Add(new RemoteAsset($"/res/sprites/arrow{i}.png?v=1_4", $"res/sprites/arrow{i}.png"));
        }
        for (var i = 1; i <= 9; i++)
        {
            assets.Add(new RemoteAsset($"/res/sprites/tool{i}.png?v=1_4", $"res/sprites/tool{i}.png"));
        }

        var shaders = new[]
        {
            "arrow-chunk.frag", "arrow-chunk.vert", "arrow.frag", "grid-generator.frag",
            "grid-tile.frag", "solid-color.frag", "sprite.vert", "vertex.vert",
        };
        assets.AddRange(shaders.Select(name =>
            new RemoteAsset($"/res/shaders/{name}?v=1_4", $"res/shaders/{name}")));

        foreach (var language in new[] { "en", "ru", "ua", "by", "fr" })
        {
            for (var i = 0; i <= 10; i++)
            {
                assets.Add(new RemoteAsset(
                    $"/res/tutorials/{language}/tutorial-{i}.html?v=1_4",
                    $"res/tutorials/{language}/tutorial-{i}.html"));
            }
        }
        for (var i = 6; i <= 8; i++)
        {
            assets.Add(new RemoteAsset($"/res/tutorials/img/lvl{i}.jpg", $"res/tutorials/img/lvl{i}.jpg"));
        }

        return assets
            .GroupBy(asset => asset.LocalPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(asset => asset.LocalPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
