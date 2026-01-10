using System;
using System.IO;
using Newtonsoft.Json;

namespace LangVPlayer.Services
{
    /// <summary>
    /// Service for saving and loading application settings.
    /// Сервис для сохранения и загрузки настроек приложения.
    /// </summary>
    public class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LangVPlayer",
            "settings.json"
        );

        /// <summary>
        /// Loads settings from JSON file.
        /// Загружает настройки из JSON файла.
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
            return new AppSettings();
        }

        /// <summary>
        /// Saves settings to JSON file.
        /// Сохраняет настройки в JSON файл.
        /// </summary>
        public static void Save(AppSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Application settings model.
    /// Модель настроек приложения.
    /// </summary>
    public class AppSettings
    {
        // Window position and size / Позиция и размер окна
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public double WindowWidth { get; set; } = 1280;
        public double WindowHeight { get; set; } = 720;
        public bool IsMaximized { get; set; } = false;

        // Always on top / Всегда поверх окон
        public bool AlwaysOnTop { get; set; } = false;

        // Volume level / Уровень громкости
        public double Volume { get; set; } = 100;

        // Last opened file / Последний открытый файл
        public string? LastVideoPath { get; set; }

        // Language / Язык интерфейса ("en", "ru" or "auto")
        public string Language { get; set; } = "auto";

        // OpenAI API Key / Ключ API OpenAI
        public string? OpenAiApiKey { get; set; }
    }
}
