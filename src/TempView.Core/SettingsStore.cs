using System.Text.Json;

namespace TempView.Core;

public sealed record SettingsLoadResult(AppSettings Settings, string? Warning);

public sealed class SettingsStore
{
    public string FilePath { get; }
    private bool futureVersion;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public SettingsStore(string? path = null) => FilePath = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TempView", "settings.json");

    public SettingsLoadResult Load()
    {
        if (!File.Exists(FilePath)) return new(new(), null);
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? throw new JsonException("Empty settings.");
            if (settings.SchemaVersion > 1)
            {
                futureVersion = true;
                return new(new(), "Konfiguracja pochodzi z nowszej wersji TempView. Plik zachowano; zapis jest zablokowany.");
            }
            var errors = SettingsValidator.Validate(settings);
            if (errors.Count > 0) throw new JsonException(string.Join(" ", errors));
            futureVersion = false;
            return new(settings, null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            string backup;
            try { backup = FilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".bak"; File.Copy(FilePath, backup); }
            catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException) { backup = "nie udało się utworzyć kopii"; }
            return new(new(), $"Nie można odczytać konfiguracji: {ex.Message} Kopia: {backup}");
        }
    }

    public void Save(AppSettings settings)
    {
        if (futureVersion) throw new InvalidOperationException("Nie można nadpisać konfiguracji z nowszej wersji.");
        var errors = SettingsValidator.Validate(settings);
        if (errors.Count > 0) throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(settings));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { JsonSerializer.Serialize(stream, settings, Options); stream.Flush(true); }
            File.Move(temporary, FilePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
