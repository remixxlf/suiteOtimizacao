using System.Diagnostics;
using System.Text.Json;
using System.IO;
using CoreIsolator.Models;

namespace CoreIsolator.Services;

public class ProfileManager
{
    private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CoreIsolator");
    private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");
    private AppSettings? _cachedSettings;

    public ProfileManager()
    {
        if (!Directory.Exists(AppDataFolder))
        {
            Directory.CreateDirectory(AppDataFolder);
        }
    }

    public AppSettings LoadSettings()
    {
        if (_cachedSettings != null) return _cachedSettings;

        if (!File.Exists(SettingsFilePath))
        {
            _cachedSettings = AppSettings.CreateDefault();
            SaveSettings(_cachedSettings);
            return _cachedSettings;
        }

        try
        {
            string json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _cachedSettings = settings ?? AppSettings.CreateDefault();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfileManager] Erro ao carregar configurações: {ex.Message}");
            _cachedSettings = AppSettings.CreateDefault();
        }

        return _cachedSettings;
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            _cachedSettings = settings;
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfileManager] Erro ao salvar configurações: {ex.Message}");
        }
    }

    public void AddProfile(GameProfile profile)
    {
        var settings = LoadSettings();
        settings.Profiles.Add(profile);
        SaveSettings(settings);
    }

    public void RemoveProfile(string gameName)
    {
        var settings = LoadSettings();
        var profile = settings.Profiles.FirstOrDefault(p => p.GameName == gameName);
        if (profile != null)
        {
            settings.Profiles.Remove(profile);
            SaveSettings(settings);
        }
    }
}
