using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace LangVPlayer.Core.Models;

/// <summary>
/// Represents a single item in the playlist.
/// Представляет один элемент в плейлисте.
/// </summary>
public class PlaylistItem : INotifyPropertyChanged
{
    private bool _isPlaying;
    private string _filePath = string.Empty;
    private string _fileName = string.Empty;
    private TimeSpan _duration;

    /// <summary>
    /// Full path to the video file / Полный путь к видеофайлу
    /// </summary>
    public string FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            _fileName = Path.GetFileName(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(FileName));
        }
    }

    /// <summary>
    /// File name without path / Имя файла без пути
    /// </summary>
    public string FileName => _fileName;

    /// <summary>
    /// Video duration / Длительность видео
    /// </summary>
    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            _duration = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DurationText));
        }
    }

    /// <summary>
    /// Formatted duration text / Форматированный текст длительности
    /// </summary>
    public string DurationText => Duration.TotalHours >= 1 
        ? Duration.ToString(@"hh\:mm\:ss") 
        : Duration.ToString(@"mm\:ss");

    /// <summary>
    /// Whether this item is currently playing / Воспроизводится ли этот элемент сейчас
    /// </summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            _isPlaying = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
