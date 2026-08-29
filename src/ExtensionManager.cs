using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogicArrowsLauncher;

/// <summary>
/// Список расширений пользователя (папки с .js-скриптами, внедряемыми в игру).
/// Расширения взаимоисключающие: активным может быть одно — «Добавить расширение»
/// и включение другого деактивируют прежнее, после чего страница игры перезагружается.
/// Хранение: extensions.json в каталоге данных лаунчера (путь задаёт платформа).
/// </summary>
public sealed class ExtensionManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string settingsPath;
    private List<ExtensionEntry> entries = new();

    public ExtensionManager(string settingsPath)
    {
        this.settingsPath = settingsPath;
        Load();
    }

    public IReadOnlyList<ExtensionEntry> Entries => entries;

    public ExtensionEntry? GetActive() => entries.FirstOrDefault(e => e.Enabled && e.Missing != true);

    /// <summary>Код активного расширения: все .js из папки в алфавитном порядке.</summary>
    public string? ReadActiveScripts()
    {
        var active = GetActive();
        if (active is null || !Directory.Exists(active.Path)) return null;
        var files = Directory.GetFiles(active.Path, "*.js", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0) return null;
        return string.Join("\n;\n", files.Select(File.ReadAllText));
    }

    /// <summary>Регистрирует папку расширения и делает её активной (остальные выключаются).</summary>
    public ExtensionEntry Register(string folderPath)
    {
        var fullPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException("Выбранная папка не найдена.");
        }
        if (Directory.GetFiles(fullPath, "*.js", SearchOption.TopDirectoryOnly).Length == 0)
        {
            throw new InvalidDataException("В выбранной папке нет .js файлов — это не расширение.");
        }

        foreach (var entry in entries)
        {
            entry.Enabled = false;
            entry.Missing = false;
        }

        var existing = entries.FirstOrDefault(e => string.Equals(e.Path, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Enabled = true;
            existing.Missing = false;
        }
        else
        {
            entries.Add(new ExtensionEntry
            {
                Name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                Path = fullPath,
                Enabled = true,
            });
        }

        Save();
        return existing ?? entries[^1];
    }

    public void SetEnabled(string name, bool enabled)
    {
        foreach (var entry in entries)
        {
            entry.Enabled = enabled && string.Equals(entry.Name, name, StringComparison.Ordinal);
        }
        Save();
    }

    public void Remove(string name)
    {
        entries.RemoveAll(e => string.Equals(e.Name, name, StringComparison.Ordinal));
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(settingsPath)) return;
            var loaded = JsonSerializer.Deserialize<List<ExtensionEntry>>(File.ReadAllText(settingsPath));
            if (loaded is not null) entries = loaded;
        }
        catch
        {
            entries = new List<ExtensionEntry>();
        }
        foreach (var entry in entries)
        {
            entry.Missing = !Directory.Exists(entry.Path);
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(entries, JsonOptions));
    }
}

public sealed class ExtensionEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Папка расширения исчезла с диска (после загрузки списка).</summary>
    [JsonPropertyName("missing")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Missing { get; set; }
}
