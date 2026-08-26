using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogicArrowsLauncher;

public sealed class MapFileEnvelope
{
    public const string ExpectedFormat = "logic-arrows-map";
    public const int ExpectedFormatVersion = 1;

    [JsonPropertyName("format")]
    public string Format { get; init; } = ExpectedFormat;

    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; init; } = ExpectedFormatVersion;

    [JsonPropertyName("siteVersion")]
    public string SiteVersion { get; init; } = ResourceCatalog.CurrentVersion;

    [JsonPropertyName("exportedAtUtc")]
    public DateTimeOffset ExportedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("mapId")]
    public string? MapId { get; init; }

    [JsonPropertyName("mapName")]
    public string? MapName { get; init; }

    [JsonPropertyName("data")]
    public string Data { get; init; } = string.Empty;
}

public static class MapFileService
{
    private const int MaxDataCharacters = 2_000_000;
    private const int MaxFileCharacters = 2_100_000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static MapFileEnvelope Read(string path)
    {
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists) throw new FileNotFoundException("Файл карты не найден.", path);
        if (fileInfo.Length > MaxFileCharacters * 2L)
        {
            throw new InvalidDataException("Файл .map слишком большой.");
        }

        return ReadText(File.ReadAllText(path));
    }

    public static MapFileEnvelope ReadText(string text)
    {
        if (text.Length > MaxFileCharacters)
        {
            throw new InvalidDataException("Файл .map слишком большой.");
        }

        MapFileEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<MapFileEnvelope>(text, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Файл .map повреждён или имеет неверный JSON.", exception);
        }

        if (envelope is null) throw new InvalidDataException("Файл .map пустой.");
        Validate(envelope);
        return envelope;
    }

    public static void Write(string path, MapFileEnvelope envelope)
    {
        Validate(envelope);
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static void Validate(MapFileEnvelope envelope)
    {
        if (!string.Equals(envelope.Format, MapFileEnvelope.ExpectedFormat, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Это не файл Logic Arrows .map.");
        }
        if (envelope.FormatVersion != MapFileEnvelope.ExpectedFormatVersion)
        {
            throw new InvalidDataException($"Неподдерживаемая версия формата .map: {envelope.FormatVersion}.");
        }
        if (!string.Equals(envelope.SiteVersion, ResourceCatalog.CurrentVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Карта создана для Logic Arrows {envelope.SiteVersion}, а лаунчер поддерживает {ResourceCatalog.CurrentVersion}.");
        }
        if (string.IsNullOrWhiteSpace(envelope.Data) || envelope.Data.Length > MaxDataCharacters)
        {
            throw new InvalidDataException("Данные карты пустые или слишком большие.");
        }

        try
        {
            var bytes = Convert.FromBase64String(envelope.Data);
            if (bytes.Length < 4) throw new InvalidDataException("Данные карты слишком короткие.");
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Данные карты не являются корректным Base64.", exception);
        }
    }
}
