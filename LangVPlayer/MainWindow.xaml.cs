using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Mpv.NET.Player;
using LangVPlayer.Services;
using LangVPlayer.Helpers;

namespace LangVPlayer;

/// <summary>
/// Main window of LangV Player application.
/// Главное окно приложения LangV Player.
/// </summary>
public partial class MainWindow : Window
{
    #region Fields / Поля

    private MpvPlayer? _mpvPlayer;
    private System.Windows.Forms.Panel? _mpvPanel;
    private AppSettings _settings;
    private DispatcherTimer? _positionTimer;
    private bool _isDraggingSlider = false;
    private bool _isFullscreen = false;
    private System.Windows.WindowState _previousWindowState;
    private double _previousWidth;
    private double _previousHeight;
    private double _previousLeft;
    private double _previousTop;

    #endregion

    #region Constructor / Конструктор

    public MainWindow()
    {
        InitializeComponent();
        
        // Load settings / Загрузка настроек
        _settings = SettingsService.Load();
        
        // Apply saved window position and size / Применение сохраненной позиции и размера окна
        ApplySettings();
        
        // Initialize position timer / Инициализация таймера позиции
        InitializePositionTimer();
    }

    #endregion

    #region Window Events / События окна

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize MPV player / Инициализация MPV плеера
        InitializeMpvPlayer();
        
        // Apply always on top setting / Применение настройки "всегда поверх"
        if (_settings.AlwaysOnTop)
        {
            PinButton.IsChecked = true;
            WindowHelper.SetAlwaysOnTop(this, true);
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save current settings / Сохранение текущих настроек
        SaveCurrentSettings();
        
        // Dispose MPV player / Освобождение ресурсов MPV
        _mpvPlayer?.Dispose();
        _positionTimer?.Stop();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Hotkeys / Горячие клавиши
        switch (e.Key)
        {
            case Key.Space:
                TogglePlayPause();
                e.Handled = true;
                break;
            case Key.Left:
                SeekRelative(-10);
                e.Handled = true;
                break;
            case Key.Right:
                SeekRelative(10);
                e.Handled = true;
                break;
            case Key.Up:
                ChangeVolume(5);
                e.Handled = true;
                break;
            case Key.Down:
                ChangeVolume(-5);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.F11:
                ToggleFullscreen();
                e.Handled = true;
                break;
            case Key.Escape:
                if (_isFullscreen)
                {
                    ToggleFullscreen();
                    e.Handled = true;
                }
                break;
        }
    }

    #endregion

    #region TitleBar Events / События заголовка окна

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to maximize/restore / Двойной клик для развертывания/восстановления
            ToggleMaximize();
        }
        else
        {
            // Drag window / Перетаскивание окна
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PinButton_Checked(object sender, RoutedEventArgs e)
    {
        _settings.AlwaysOnTop = true;
        WindowHelper.SetAlwaysOnTop(this, true);
    }

    private void PinButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _settings.AlwaysOnTop = false;
        WindowHelper.SetAlwaysOnTop(this, false);
    }

    #endregion

    #region Video Controls / Управление видео

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenVideoFile();
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayPause();
    }

    private void RewindButton_Click(object sender, RoutedEventArgs e)
    {
        SeekRelative(-10);
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        SeekRelative(10);
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_mpvPlayer != null)
        {
            _mpvPlayer.Volume = (int)e.NewValue;
            _settings.Volume = e.NewValue;
            UpdateVolumeIcon();
        }
    }

    private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingSlider && _mpvPlayer != null)
        {
            // Don't seek while dragging, wait for mouse up
            // Не перематываем во время перетаскивания, ждем отпускания мыши
        }
    }

    private void TimelineSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void TimelineSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_mpvPlayer != null && _mpvPlayer.Duration.TotalSeconds > 0)
        {
            var seekPosition = TimeSpan.FromSeconds(TimelineSlider.Value);
            _mpvPlayer.SeekAsync(seekPosition);
        }
        _isDraggingSlider = false;
    }

    #endregion

    #region MPV Player Methods / Методы MPV плеера

    private void InitializeMpvPlayer()
    {
        try
        {
            // Create a WinForms Panel to host MPV / Создаем WinForms Panel для размещения MPV
            _mpvPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.Black
            };
            MpvHost.Child = _mpvPanel;

            // Create MPV player with the panel's handle / Создание MPV плеера с дескриптором панели
            _mpvPlayer = new MpvPlayer(_mpvPanel.Handle)
            {
                Volume = (int)_settings.Volume
            };

            VolumeSlider.Value = _settings.Volume;
            UpdateVolumeIcon();

            // Event handlers / Обработчики событий
            _mpvPlayer.MediaLoaded += MpvPlayer_MediaLoaded;
            _mpvPlayer.MediaFinished += MpvPlayer_MediaFinished;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to initialize MPV player.\nОшибка инициализации MPV плеера.\n\n{ex.Message}",
                "Error / Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void MpvPlayer_MediaLoaded(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_mpvPlayer != null)
            {
                TimelineSlider.Maximum = _mpvPlayer.Duration.TotalSeconds;
                TotalTimeText.Text = FormatTime(_mpvPlayer.Duration);
                NoVideoPlaceholder.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void MpvPlayer_MediaFinished(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            PlayPauseButton.Content = "\uE768"; // Play icon
        });
    }

    private void OpenVideoFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open Video / Открыть видео",
            Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadVideo(dialog.FileName);
        }
    }

    private void LoadVideo(string filePath)
    {
        try
        {
            _mpvPlayer?.Load(filePath);
            _settings.LastVideoPath = filePath;
            PlayPauseButton.Content = "\uE769"; // Pause icon
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to load video.\nОшибка загрузки видео.\n\n{ex.Message}",
                "Error / Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void TogglePlayPause()
    {
        if (_mpvPlayer == null) return;

        if (_mpvPlayer.IsPlaying)
        {
            _mpvPlayer.Pause();
            PlayPauseButton.Content = "\uE768"; // Play icon
        }
        else
        {
            _mpvPlayer.Resume();
            PlayPauseButton.Content = "\uE769"; // Pause icon
        }
    }

    private void SeekRelative(double seconds)
    {
        if (_mpvPlayer == null || _mpvPlayer.Duration.TotalSeconds == 0) return;

        var newPosition = _mpvPlayer.Position.TotalSeconds + seconds;
        newPosition = Math.Max(0, Math.Min(newPosition, _mpvPlayer.Duration.TotalSeconds));
        _mpvPlayer.SeekAsync(TimeSpan.FromSeconds(newPosition));
    }

    private void ChangeVolume(int delta)
    {
        var newVolume = Math.Max(0, Math.Min(100, VolumeSlider.Value + delta));
        VolumeSlider.Value = newVolume;
    }

    #endregion

    #region UI Helper Methods / Вспомогательные методы UI

    private void InitializePositionTimer()
    {
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _positionTimer.Tick += PositionTimer_Tick;
        _positionTimer.Start();
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (_mpvPlayer != null && !_isDraggingSlider)
        {
            TimelineSlider.Value = _mpvPlayer.Position.TotalSeconds;
            CurrentTimeText.Text = FormatTime(_mpvPlayer.Position);
        }
    }

    private void UpdateVolumeIcon()
    {
        if (VolumeSlider.Value == 0)
            VolumeIcon.Text = "\uE74F"; // Muted
        else if (VolumeSlider.Value < 33)
            VolumeIcon.Text = "\uE993"; // Low
        else if (VolumeSlider.Value < 66)
            VolumeIcon.Text = "\uE994"; // Medium
        else
            VolumeIcon.Text = "\uE767"; // High
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.Hours > 0
            ? $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes:D2}:{time.Seconds:D2}";
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeButton.Content = "\uE922"; // Maximize icon
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeButton.Content = "\uE923"; // Restore icon
        }
    }

    private void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            // Exit fullscreen / Выход из полноэкранного режима
            WindowState = _previousWindowState;
            Width = _previousWidth;
            Height = _previousHeight;
            Left = _previousLeft;
            Top = _previousTop;
            ControlPanel.Visibility = Visibility.Visible;
            FullscreenButton.Content = "\uE740";
            _isFullscreen = false;
        }
        else
        {
            // Enter fullscreen / Вход в полноэкранный режим
            _previousWindowState = WindowState;
            _previousWidth = Width;
            _previousHeight = Height;
            _previousLeft = Left;
            _previousTop = Top;
            WindowState = WindowState.Maximized;
            ControlPanel.Visibility = Visibility.Collapsed;
            FullscreenButton.Content = "\uE73F";
            _isFullscreen = true;
        }
    }

    #endregion

    #region Settings Methods / Методы настроек

    private void ApplySettings()
    {
        // Apply window position and size / Применение позиции и размера окна
        if (_settings.WindowLeft >= 0 && _settings.WindowTop >= 0)
        {
            Left = _settings.WindowLeft;
            Top = _settings.WindowTop;
        }
        
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;

        if (_settings.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SaveCurrentSettings()
    {
        // Save window state / Сохранение состояния окна
        _settings.IsMaximized = WindowState == WindowState.Maximized;

        if (WindowState == WindowState.Normal)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
        }

        SettingsService.Save(_settings);
    }

    #endregion
}