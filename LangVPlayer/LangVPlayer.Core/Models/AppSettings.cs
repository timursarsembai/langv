namespace LangVPlayer.Core.Models;

/// <summary>
/// Base application settings model.
/// Базовая модель настроек приложения.
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
}

/// <summary>
/// Extended settings for LangV Player with AI and dictionary features.
/// Расширенные настройки для LangV Player с функциями ИИ и словарей.
/// </summary>
public class LangVSettings : AppSettings
{
    // OpenAI API Key / Ключ API OpenAI
    public string? OpenAiApiKey { get; set; }
    
    // Path to custom dictionary / Путь к пользовательскому словарю
    public string? CustomDictionaryPath { get; set; }
    
    // Auto-pause on word hover / Авто-пауза при наведении на слово
    public bool AutoPauseOnHover { get; set; } = true;
}
