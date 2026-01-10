using System;
using System.IO;
using LangVPlayer.Core.Models;
using Newtonsoft.Json;

namespace LangVPlayer.Core.Services;

/// <summary>
/// Service for saving and loading application settings.
/// Сервис для сохранения и загрузки настроек приложения.
/// </summary>
public class SettingsService<T> where T : AppSettings, new()
{
    private readonly string _settingsPath;

    /// <summary>
    /// Create settings service with custom app folder name.
    /// Создать сервис настроек с пользовательским именем папки.
    /// </summary>
    /// <param name="appFolderName">Application folder name / Имя папки приложения</param>
    public SettingsService(string appFolderName)
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            appFolderName,
            "settings.json"
        );
    }

    /// <summary>
    /// Loads settings from JSON file.
    /// Загружает настройки из JSON файла.
    /// </summary>
    public T Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonConvert.DeserializeObject<T>(json) ?? new T();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
        return new T();
    }

    /// <summary>
    /// Saves settings to JSON file.
    /// Сохраняет настройки в JSON файл.
    /// </summary>
    public void Save(T settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}
