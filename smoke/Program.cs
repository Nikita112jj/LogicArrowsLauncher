using LogicArrowsLauncher;

var root = Path.Combine(Directory.GetCurrentDirectory(), "smoke-updates");
if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
var mapPath = Path.Combine(Directory.GetCurrentDirectory(), "smoke-test.map");
if (File.Exists(mapPath)) File.Delete(mapPath);
var store = new UpdateStore(Path.Combine(root, "updates"));
var progress = new Progress<SyncProgress>(_ => { });

using var first = new AssetSynchronizer(store);
var firstSummary = await first.SyncAsync(progress, CancellationToken.None);

using var second = new AssetSynchronizer(store);
var secondSummary = await second.SyncAsync(progress, CancellationToken.None);

Console.WriteLine($"assets={ResourceCatalog.All.Count}");
Console.WriteLine($"first_downloaded={firstSummary.Downloaded}, first_not_modified={firstSummary.NotModified}, first_failed={firstSummary.Failed}");
Console.WriteLine($"second_checked={secondSummary.Checked}, second_downloaded={secondSummary.Downloaded}, second_not_modified={secondSummary.NotModified}, second_failed={secondSummary.Failed}, second_fast={secondSummary.FastVersionChecked}");
Console.WriteLine($"first_required={first.HasRequiredCache()}");
Console.WriteLine($"second_required={second.HasRequiredCache()}");
var versionsDirectory = Path.Combine(root, "updates", "versions");
var versionCount = Directory.Exists(versionsDirectory)
    ? Directory.GetDirectories(versionsDirectory).Length
    : 0;
Console.WriteLine($"update_directory=updates");
Console.WriteLine($"saved_version_count={versionCount}");

if (!first.HasRequiredCache() || !second.HasRequiredCache()) return 2;
if (firstSummary.Failed > 0 || secondSummary.Failed > 0) return 3;
if (firstSummary.Downloaded <= 0) return 4;
if (secondSummary.Downloaded != 0 ||
    secondSummary.NotModified != ResourceCatalog.VersionSentinels.Count ||
    secondSummary.Checked != ResourceCatalog.VersionSentinels.Count ||
    !secondSummary.FastVersionChecked) return 5;
if (versionCount != 1) return 8;
if (!second.TryGetAsset("/bundle.js?v=1_4", out var bundle, out _)) return 6;
if (bundle.Length == 0) return 7;

var envelope = new MapFileEnvelope
{
    MapId = "local-smoke",
    MapName = "Smoke test",
    Data = Convert.ToBase64String(new byte[] { 0, 1, 2, 3 }),
};
MapFileService.Write(mapPath, envelope);
var roundTrip = MapFileService.Read(mapPath);
var textRoundTrip = MapFileService.ReadText(File.ReadAllText(mapPath));
Console.WriteLine($"map_round_trip={roundTrip.Data == envelope.Data}");
Console.WriteLine($"map_text_round_trip={textRoundTrip.Data == envelope.Data}");
if (roundTrip.Data != envelope.Data || textRoundTrip.Data != envelope.Data || roundTrip.SiteVersion != ResourceCatalog.CurrentVersion) return 9;
File.WriteAllText(mapPath, "{\"format\":\"wrong\",\"formatVersion\":1,\"siteVersion\":\"1_4\",\"data\":\"AAECAw==\"}");
try
{
    _ = MapFileService.Read(mapPath);
    return 10;
}
catch (InvalidDataException)
{
    Console.WriteLine("map_invalid_rejected=True");
}
finally
{
    if (File.Exists(mapPath)) File.Delete(mapPath);
}
return 0;
