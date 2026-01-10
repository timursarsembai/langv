using System.Globalization;

namespace LangVPlayer.Resources;

public static class Strings
{
    public static string File { get; private set; } = "File";
    public static string OpenFile { get; private set; } = "Open File...";
    public static string Exit { get; private set; } = "Exit";
    public static string Playback { get; private set; } = "Playback";
    public static string Play { get; private set; } = "Play";
    public static string PlayPauseTooltip { get; private set; } = "Play/Pause";
    public static string RewindTooltip { get; private set; } = "Rewind 10s";
    public static string ForwardTooltip { get; private set; } = "Forward 10s";
    public static string Pause { get; private set; } = "Pause";
    
    // Placeholder
    public static string DropVideo { get; private set; } = "Drop video file here or click to open";
    public static string OpenVideo { get; private set; } = "Open Video";
    
    // Speed
    public static string SlowerTooltip { get; private set; } = "Slower";
    public static string FasterTooltip { get; private set; } = "Faster";
    public static string ResetSpeedTooltip { get; private set; } = "Reset speed";
    public static string Stop { get; private set; } = "Stop";
    public static string Rewind { get; private set; } = "Rewind (-10s)";
    public static string Forward { get; private set; } = "Forward (+10s)";
    public static string VolumeUp { get; private set; } = "Volume Up";
    public static string VolumeDown { get; private set; } = "Volume Down";
    public static string Speed { get; private set; } = "Speed";
    public static string SpeedUp { get; private set; } = "Increase Speed";
    public static string SpeedDown { get; private set; } = "Decrease Speed";
    public static string SpeedReset { get; private set; } = "Reset (1.0x)";
    public static string Audio { get; private set; } = "Audio";
    public static string AudioTrack { get; private set; } = "Audio Track";
    public static string NoAudioTracks { get; private set; } = "(no tracks)";
    public static string Mute { get; private set; } = "Mute";
    public static string Unmute { get; private set; } = "Unmute";
    public static string View { get; private set; } = "View";
    public static string Fullscreen { get; private set; } = "Fullscreen";
    public static string CompactMode { get; private set; } = "Compact Mode";
    public static string ExitCompactMode { get; private set; } = "Exit Compact Mode";
    public static string AlwaysOnTop { get; private set; } = "Always on Top";
    public static string Minimize { get; private set; } = "Minimize";
    public static string Maximize { get; private set; } = "Maximize";
    public static string Restore { get; private set; } = "Restore";
    public static string Subtitles { get; private set; } = "Subtitles";
    public static string EmbeddedSubs1 { get; private set; } = "Embedded (Slot 1)";
    public static string EmbeddedSubs2 { get; private set; } = "Embedded (Slot 2)";
    public static string Disable { get; private set; } = "Disable";
    public static string LoadExternalSubs1 { get; private set; } = "Load External Subtitles 1...";
    public static string LoadExternalSubs2 { get; private set; } = "Load External Subtitles 2...";
    public static string Subs1None { get; private set; } = "Subtitles 1: (none)";
    public static string Subs2None { get; private set; } = "Subtitles 2: (none)";
    public static string Subtitle1Prefix { get; private set; } = "Subtitles 1: ";
    public static string Subtitle2Prefix { get; private set; } = "Subtitles 2: ";
    public static string SelectSubtitleFile1 { get; private set; } = "Select Subtitle File 1";
    public static string SelectSubtitleFile2 { get; private set; } = "Select Subtitle File 2";
    public static string ClearAllSubs { get; private set; } = "Clear All Subtitles";
    public static string Help { get; private set; } = "Help";
    public static string About { get; private set; } = "About...";
    public static string AppTitle { get; private set; } = "LangV Player";
    
    // Tooltips
    public static string AlwaysOnTopTooltip { get; private set; } = "Always on Top";
    public static string MinimizeTooltip { get; private set; } = "Minimize";
    public static string MaximizeTooltip { get; private set; } = "Maximize";
    public static string CloseTooltip { get; private set; } = "Close";
    public static string MuteTooltip { get; private set; } = "Mute";
    public static string PlaylistTooltip { get; private set; } = "Playlist";
    public static string CompactModeTooltip { get; private set; } = "Compact Mode (P)";
    public static string FullscreenTooltip { get; private set; } = "Fullscreen";
    public static string AddFilesTooltip { get; private set; } = "Add files";
    public static string ClearPlaylistTooltip { get; private set; } = "Clear playlist";
    
    // System messages
    public static string Error { get; private set; } = "Error";
    public static string ErrorLoadingVideo { get; private set; } = "Failed to load video.";
    public static string VideoNotFound { get; private set; } = "Video file not found";
    public static string Language { get; private set; } = "Language";
    public static string English { get; private set; } = "English";
    public static string Russian { get; private set; } = "Russian";
    public static string RestartRequired { get; private set; } = "Restart Required";
    public static string RestartToApplyLanguage { get; private set; } = "Please restart application for language changes to take effect.";

    public static void Init(string? forcedLanguage = null)
    {
        string lang;
        
        if (forcedLanguage == "auto" || string.IsNullOrEmpty(forcedLanguage))
        {
            // Detect system language / Определить системный язык
            lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }
        else
        {
            lang = forcedLanguage;
        }
        
        if (lang.Equals("ru", StringComparison.OrdinalIgnoreCase))
        {
            SetRussian();
        }
        else
        {
            SetEnglish(); // Default
        }
    }

    private static void SetEnglish()
    {
        Language = "Language";
        English = "English";
        Russian = "Русский";
        RestartRequired = "Restart Required";
        RestartToApplyLanguage = "Please restart application for language changes to take effect.";
        File = "File";
        OpenFile = "Open File...";
        Exit = "Exit";
        Playback = "Playback";
        Play = "Play";
        Pause = "Pause";
        Stop = "Stop";
        Rewind = "Rewind (-10s)";
        Forward = "Forward (+10s)";
        VolumeUp = "Volume Up";
        VolumeDown = "Volume Down";
        Speed = "Speed";
        SpeedUp = "Increase Speed";
        SpeedDown = "Decrease Speed";
        SpeedReset = "Reset (1.0x)";
        Audio = "Audio";
        AudioTrack = "Audio Track";
        Mute = "Mute";
        Unmute = "Unmute";
        View = "View";
        Fullscreen = "Fullscreen";
        CompactMode = "Compact Mode";
        AlwaysOnTop = "Always on Top";
        Minimize = "Minimize";
        Maximize = "Maximize";
        Restore = "Restore";
        Subtitles = "Subtitles";
        EmbeddedSubs1 = "Embedded (Slot 1)";
        EmbeddedSubs2 = "Embedded (Slot 2)";
        Disable = "Disable";
        LoadExternalSubs1 = "Load External Subtitles 1...";
        LoadExternalSubs2 = "Load External Subtitles 2...";
        Subs1None = "Subtitles 1: (none)";
        Subs2None = "Subtitles 2: (none)";
        ClearAllSubs = "Clear All Subtitles";
        Help = "Help";
        About = "About...";
        AppTitle = "LangV Player";
        Error = "Error";
        VideoNotFound = "Video file not found";
        
        // Tooltips
        PlayPauseTooltip = "Play/Pause";
        RewindTooltip = "Rewind 10s";
        ForwardTooltip = "Forward 10s";
        SlowerTooltip = "Slower (Shift+[)";
        FasterTooltip = "Faster (Shift+])";
        ResetSpeedTooltip = "Reset speed";
        AlwaysOnTopTooltip = "Always on Top";
        MinimizeTooltip = "Minimize";
        MaximizeTooltip = "Maximize";
        CloseTooltip = "Close";
        MuteTooltip = "Mute";
        PlaylistTooltip = "Playlist";
        CompactModeTooltip = "Compact Mode (P)";
        FullscreenTooltip = "Fullscreen";
        AddFilesTooltip = "Add files";
        ClearPlaylistTooltip = "Clear playlist";
        
        // Placeholder
        DropVideo = "Drop video file here or click to open";
        OpenVideo = "📁 Open Video";
    }

    private static void SetRussian()
    {
        Language = "Язык";
        English = "English";
        Russian = "Русский";
        RestartRequired = "Требуется перезапуск";
        RestartToApplyLanguage = "Пожалуйста, перезапустите приложение, чтобы изменения вступили в силу.";
        File = "Файл";
        OpenFile = "Открыть файл...";
        Exit = "Выход";
        Playback = "Воспроизведение";
        Play = "Воспроизвести";
        PlayPauseTooltip = "Воспроизведение/Пауза";
        RewindTooltip = "Назад 10с";
        ForwardTooltip = "Вперед 10с";
        Pause = "Пауза";
        DropVideo = "Перетащите видеофайл сюда или нажмите для открытия";
        OpenVideo = "Открыть видео";
        SlowerTooltip = "Медленнее";
        FasterTooltip = "Быстрее";
        ResetSpeedTooltip = "Сбросить скорость";
        Stop = "Стоп";
        Rewind = "Назад (-10с)";
        Forward = "Вперёд (+10с)";
        VolumeUp = "Громкость +";
        VolumeDown = "Громкость -";
        Speed = "Скорость";
        SpeedUp = "Увеличить скорость";
        SpeedDown = "Уменьшить скорость";
        SpeedReset = "Сбросить (1.0x)";
        Audio = "Аудио";
        AudioTrack = "Аудиодорожка";
        NoAudioTracks = "(нет дорожек)";
        Mute = "Выключить звук";
        Unmute = "Включить звук";
        View = "Вид";
        Fullscreen = "На полный экран";
        CompactMode = "Компактный режим";
        ExitCompactMode = "Выйти из компактного режима";
        AlwaysOnTop = "Поверх всех окон";
        Minimize = "Свернуть";
        Maximize = "Развернуть";
        Restore = "Восстановить";
        Subtitles = "Субтитры";
        EmbeddedSubs1 = "Встроенные (слот 1)";
        EmbeddedSubs2 = "Встроенные (слот 2)";
        Disable = "Отключить";
        LoadExternalSubs1 = "Загрузить внешние субтитры 1...";
        LoadExternalSubs2 = "Загрузить внешние субтитры 2...";
        Subs1None = "Субтитры 1: (нет)";
        Subs2None = "Субтитры 2: (нет)";
        Subtitle1Prefix = "Субтитры 1: ";
        Subtitle2Prefix = "Субтитры 2: ";
        SelectSubtitleFile1 = "Выберите файл субтитров 1";
        SelectSubtitleFile2 = "Выберите файл субтитров 2";
        ClearAllSubs = "Очистить все субтитры";
        Help = "Справка";
        About = "О программе...";
        AppTitle = "LangV Player";
        Error = "Ошибка";
        ErrorLoadingVideo = "Ошибка загрузки видео.";
        VideoNotFound = "Видеофайл не найден";
        
        // Tooltips
        PlayPauseTooltip = "Воспроизведение/Пауза";
        RewindTooltip = "Назад 10с";
        ForwardTooltip = "Вперед 10с";
        SlowerTooltip = "Медленнее (Shift+[)";
        FasterTooltip = "Быстрее (Shift+])";
        ResetSpeedTooltip = "Сбросить скорость";
        AlwaysOnTopTooltip = "Поверх всех окон";
        MinimizeTooltip = "Свернуть";
        MaximizeTooltip = "Развернуть";
        CloseTooltip = "Закрыть";
        MuteTooltip = "Отключить звук";
        PlaylistTooltip = "Плейлист";
        CompactModeTooltip = "Компактный режим (P)";
        FullscreenTooltip = "Полный экран";
        AddFilesTooltip = "Добавить файлы";
        ClearPlaylistTooltip = "Очистить плейлист";
        
        // Placeholder
        DropVideo = "Перетащите видеофайл сюда или нажмите для открытия";
        OpenVideo = "📁 Открыть видео";
    }
}
