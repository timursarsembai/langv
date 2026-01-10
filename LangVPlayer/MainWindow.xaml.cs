using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LangVPlayer.Services;
using LangVPlayer.Helpers;
using LangVPlayer.Models;

namespace LangVPlayer;

/// <summary>
/// Main window of LangV Player application.
/// Главное окно приложения LangV Player.
/// </summary>
public partial class MainWindow : Window
{
    #region Fields / Поля

    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia; // Current media object, must be disposed / Текущий объект Media, должен быть освобождён
    private AppSettings _settings;
    private DispatcherTimer? _controlPanelTimer;
    private bool _isDraggingSlider = false;
    private long _pendingSeekPosition = -1; // Position we're seeking to, blocks TimeChanged updates / Позиция к которой перематываем, блокирует обновления TimeChanged
    private bool _isFullscreen = false;
    private bool _isMuted = false;
    private double _volumeBeforeMute = 100;
    private string? _currentVideoPath;
    private System.Windows.WindowState _previousWindowState;
    private double _previousWidth;
    private double _previousHeight;
    private double _previousLeft;
    private double _previousTop;
    
    // Playlist / Плейлист
    private ObservableCollection<PlaylistItem> _playlist = new();
    private int _currentPlaylistIndex = -1;
    
    // Playback Speed / Скорость воспроизведения
    private float _currentSpeed = 1.0f;
    private readonly float[] _speedPresets = { 0.25f, 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 1.75f, 2.0f };
    
    // Closing flag to prevent UI updates during shutdown / Флаг закрытия для предотвращения обновлений UI при завершении
    private volatile bool _isClosing = false;
    
    // Thumbnail preview / Превью миниатюр
    private ThumbnailService? _thumbnailService;
    private CancellationTokenSource? _thumbnailCts;
    private DispatcherTimer? _thumbnailDebounceTimer;
    private long _lastThumbnailTimeMs = -1;
    private bool _isDraggingThumb = false;
    
    // Compact/PiP mode / Компактный режим
    private bool _isCompactMode = false;
    private double _compactPreviousWidth;
    private double _compactPreviousHeight;
    private double _compactPreviousLeft;
    private double _compactPreviousTop;
    private System.Windows.WindowState _compactPreviousWindowState;
    private bool _compactPreviousPinState = false;
    
    // Compact mode settings / Настройки компактного режима
    private double _compactWidth = 400;
    private double _compactHeight = 225; // 16:9 aspect ratio
    
    // Subtitles / Субтитры
    private List<Models.SubtitleItem> _primarySubtitles = new();
    private List<Models.SubtitleItem> _secondarySubtitles = new();
    private string? _primarySubtitleSource; // Path or embedded track name / Путь или название встроенной дорожки
    private string? _secondarySubtitleSource;
    private bool _showPrimarySubtitles = true;
    private bool _showSecondarySubtitles = true;
    private DispatcherTimer? _subtitleTimer;
    private Models.SubtitleItem? _currentPrimarySubtitle;
    private Models.SubtitleItem? _currentSecondarySubtitle;
    
    // Embedded subtitle extraction / Извлечение встроенных субтитров
    private Services.SubtitleExtractorService? _subtitleExtractor;
    private List<Services.SubtitleExtractorService.SubtitleTrackInfo> _embeddedSubtitleTracks = new();
    private int _selectedEmbeddedTrackSlot1 = -1; // Selected stream index for slot 1 / Выбранный индекс потока для слота 1
    private int _selectedEmbeddedTrackSlot2 = -1; // Selected stream index for slot 2 / Выбранный индекс потока для слота 2

    #endregion

    #region Constructor / Конструктор

    public MainWindow()
    {
        InitializeComponent();
        
        // Load settings / Загрузка настроек
        _settings = SettingsService.Load();
        
        // Apply saved window position and size / Применение сохраненной позиции и размера окна
        ApplySettings();
        
        // Initialize control panel hide timer / Инициализация таймера скрытия панели управления
        InitializeControlPanelTimer();
        
        // Initialize thumbnail service (graceful - will disable if FFmpeg not found)
        // Инициализация сервиса миниатюр (graceful - отключится если FFmpeg не найден)
        _thumbnailService = new ThumbnailService();
        
        // Initialize subtitle extractor (uses same FFmpeg path)
        // Инициализация сервиса извлечения субтитров (использует тот же путь к FFmpeg)
        var ffmpegPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        _subtitleExtractor = new Services.SubtitleExtractorService(ffmpegPath);
        
        // Initialize thumbnail debounce timer (150ms delay before generating)
        // Инициализация таймера debounce для миниатюр (150мс задержка перед генерацией)
        _thumbnailDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _thumbnailDebounceTimer.Tick += ThumbnailDebounceTimer_Tick;
        
        // Initialize playlist / Инициализация плейлиста
        PlaylistListBox.ItemsSource = _playlist;
        
        // Initialize subtitle sync timer (100ms for smooth subtitle display)
        // Инициализация таймера синхронизации субтитров (100мс для плавного отображения)
        _subtitleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _subtitleTimer.Tick += SubtitleTimer_Tick;
        
        // Register timeline slider click handler with handledEventsToo = true
        // Регистрация обработчика клика по слайдеру с handledEventsToo = true
        TimelineSlider.AddHandler(
            System.Windows.Controls.Primitives.Track.MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(TimelineSlider_TrackMouseDown),
            true); // handledEventsToo = true - catch even handled events
        
        // Set focus on Loaded to enable keyboard shortcuts / Установить фокус при загрузке для работы горячих клавиш
        this.Loaded += (s, e) => { this.Focus(); };
        
        // Apply AlwaysOnTop after window handle is created / Применить AlwaysOnTop после создания дескриптора окна
        this.SourceInitialized += (s, e) =>
        {
            if (_settings.AlwaysOnTop)
            {
                this.Topmost = true;
                PinButton.IsChecked = true;
                MenuAlwaysOnTop.IsChecked = true;
                WindowHelper.SetAlwaysOnTop(this, true);
            }
        };
    }

    #endregion

    #region Window Events / События окна

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize VLC player / Инициализация VLC плеера
        InitializeVlcPlayer();
        
        // Subscribe to Activated event to ensure hotkeys work / 
        // Подписаться на событие Activated для работы горячих клавиш
        this.Activated += Window_Activated;
        
        // Note: AlwaysOnTop is applied in SourceInitialized event (constructor) where hwnd is guaranteed
        // Примечание: AlwaysOnTop применяется в событии SourceInitialized (конструктор) где hwnd гарантирован
    }
    
    private void Window_Activated(object? sender, EventArgs e)
    {
        // Always ensure window has focus for hotkeys when activated / 
        // Всегда обеспечить фокус для горячих клавиш при активации
        // But NOT if any menu is open - it would close the menu / 
        // Но НЕ если открыто какое-либо меню - это закроет меню
        if (!_isClosing)
        {
            // Check if any popup/menu is open / Проверить, открыто ли какое-либо всплывающее окно/меню
            var focusedElement = Keyboard.FocusedElement as DependencyObject;
            if (focusedElement != null)
            {
                var parent = focusedElement;
                while (parent != null)
                {
                    if (parent is System.Windows.Controls.MenuItem || 
                        parent is System.Windows.Controls.Primitives.Popup)
                    {
                        return; // Don't steal focus from menu / Не забирать фокус у меню
                    }
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
            }
            
            this.Focus();
        }
    }
    
    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Ensure window has keyboard focus on any mouse click, but skip for Menu items
        // Обеспечить фокус клавиатуры при любом клике мыши, но пропустить для элементов Menu
        if (_isClosing) return;
        
        // Don't steal focus from Menu - it breaks menu item clicks
        // Не забирать фокус у Menu - это ломает клики по пунктам меню
        var source = e.OriginalSource as DependencyObject;
        while (source != null)
        {
            if (source is System.Windows.Controls.Menu || 
                source is System.Windows.Controls.MenuItem ||
                source is System.Windows.Controls.Primitives.Popup)
            {
                return; // Let menu handle the click / Пусть меню обработает клик
            }
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        
        this.Focus();
    }

    /// <summary>
    /// Adapts subtitle font size based on window size.
    /// Адаптирует размер шрифта субтитров в зависимости от размера окна.
    /// </summary>
    private void UpdateSubtitleFontSize()
    {
        double height = this.ActualHeight;
        
        // Check if we're in compact mode or small window / Проверить, в компактном ли режиме или маленьком окне
        // We use ActualHeight of the Window here, whereas previously we used SubtitleOverlay height which was wrong
        // Используем ActualHeight окна, так как ранее использовали высоту SubtitleOverlay, что было ошибочно
        bool isSmallWindow = _isCompactMode || height < 350;
        
        if (isSmallWindow)
        {
            // Compact mode - smaller subtitles / Компактный режим - меньшие субтитры
            double scale = Math.Max(0.5, height / 350.0);
            scale = Math.Min(1.0, scale);
            
            double primarySize = 14.0 * scale; // 14px base for compact / 14px база для компактного
            double secondarySize = 12.0 * scale; // 12px base for compact / 12px база для компактного
            
            // Minimum sizes / Минимальные размеры
            primarySize = Math.Max(10.0, primarySize);
            secondarySize = Math.Max(9.0, secondarySize);
            
            PrimarySubtitleText.FontSize = primarySize;
            SecondarySubtitleText.FontSize = secondarySize;
            
            // Smaller margins and padding for compact / Меньшие отступы для компактного
            SubtitleOverlay.Margin = new Thickness(5, 0, 5, 15);
            PrimarySubtitleBorder.Padding = new Thickness(4, 2, 4, 2);
            SecondarySubtitleBorder.Padding = new Thickness(3, 1, 3, 1);
            PrimarySubtitleBorder.MaxHeight = height * 0.4; // More flexible height
            SecondarySubtitleBorder.MaxHeight = height * 0.3;
        }
        else
        {
            // Normal/fullscreen mode - standard sizes / Обычный/полноэкранный режим - стандартные размеры
            const double normalPrimaryFontSize = 22.0;
            const double normalSecondaryFontSize = 18.0;

            PrimarySubtitleText.FontSize = normalPrimaryFontSize;
            SecondarySubtitleText.FontSize = normalSecondaryFontSize;
            
            // Standard margins and padding / Стандартные отступы
            SubtitleOverlay.Margin = new Thickness(20, 0, 20, 60);
            PrimarySubtitleBorder.Padding = new Thickness(12, 6, 12, 6);
            SecondarySubtitleBorder.Padding = new Thickness(10, 4, 10, 4);
            PrimarySubtitleBorder.MaxHeight = 150;
            SecondarySubtitleBorder.MaxHeight = 100;
        }
    }

    /// <summary>
    /// Window size changed handler - saves compact mode size / Обработчик изменения размера окна
    /// </summary>
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Update subtitle sizes / Обновить размеры субтитров
        UpdateSubtitleFontSize();

        // If in compact mode, remember the new size for next time / 
        // Если в компактном режиме, запомнить новый размер
        if (_isCompactMode)
        {
            _compactWidth = ActualWidth;
            _compactHeight = ActualHeight;
        }
    }
    
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Set closing flag immediately to stop all UI updates
        // Установить флаг закрытия немедленно, чтобы остановить все обновления UI
        _isClosing = true;
        
        // Cancel and dispose thumbnail CTS / Отменить и освободить CTS миниатюр
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
        
        // Stop thumbnail debounce timer / Остановить таймер debounce миниатюр
        _thumbnailDebounceTimer?.Stop();
        
        // Save current settings / Сохранение текущих настроек
        SaveCurrentSettings();
        
        // Stop hide timer / Остановить таймер скрытия
        _controlPanelTimer?.Stop();
        
        // Stop subtitle timer / Остановить таймер субтитров
        _subtitleTimer?.Stop();
        
        // Dispose thumbnail service / Освободить сервис миниатюр
        _thumbnailService?.Dispose();
        
        // Dispose VLC player safely / Безопасное освобождение ресурсов VLC
        if (_mediaPlayer != null)
        {
            // Unsubscribe from events first to prevent callbacks during disposal
            // Сначала отписываемся от событий, чтобы предотвратить обратные вызовы при освобождении
            _mediaPlayer.Playing -= MediaPlayer_Playing;
            _mediaPlayer.Paused -= MediaPlayer_Paused;
            _mediaPlayer.Stopped -= MediaPlayer_Stopped;
            _mediaPlayer.EndReached -= MediaPlayer_EndReached;
            _mediaPlayer.LengthChanged -= MediaPlayer_LengthChanged;
            _mediaPlayer.TimeChanged -= MediaPlayer_TimeChanged;
            
            // Stop playback asynchronously to avoid UI thread blocking
            // Асинхронная остановка воспроизведения, чтобы избежать блокировки UI потока
            try
            {
                if (_mediaPlayer.IsPlaying)
                {
                    _mediaPlayer.Stop();
                }
            }
            catch { /* Ignore stop errors / Игнорировать ошибки остановки */ }
        }
        
        // Dispose in correct order / Освобождение в правильном порядке
        try { _currentMedia?.Dispose(); } catch { }
        try { _mediaPlayer?.Dispose(); } catch { }
        try { _libVLC?.Dispose(); } catch { }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Hotkeys / Горячие клавиши
        switch (e.Key)
        {
            case Key.Space:
                TogglePlayPause();
                e.Handled = true;
                break;
            case Key.Left:
                SeekRelative(-10000); // milliseconds
                e.Handled = true;
                break;
            case Key.Right:
                SeekRelative(10000); // milliseconds
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
            case Key.M:
                ToggleMute();
                e.Handled = true;
                break;
            case Key.Escape:
                if (_isCompactMode)
                {
                    ToggleCompactMode();
                    e.Handled = true;
                }
                else if (_isFullscreen)
                {
                    ToggleFullscreen();
                    e.Handled = true;
                }
                break;
            case Key.P:
                ToggleCompactMode();
                e.Handled = true;
                break;
            // Speed controls / Управление скоростью
            case Key.OemOpenBrackets: // '[' key
                ChangeSpeed(-0.25f);
                e.Handled = true;
                break;
            case Key.OemCloseBrackets: // ']' key
                ChangeSpeed(0.25f);
                e.Handled = true;
                break;
            case Key.Back: // Backspace - reset speed
                SetSpeed(1.0f);
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Video Overlay Events / События прозрачного слоя видео

    private void VideoOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Return focus to window for hotkeys / Вернуть фокус на окно для горячих клавиш
        this.Focus();
        
        // In compact mode: single click to drag, double-click to exit compact mode
        // В компактном режиме: один клик для перетаскивания, двойной для выхода
        if (_isCompactMode)
        {
            if (e.ClickCount == 2)
            {
                ToggleCompactMode();
                e.Handled = true;
            }
            else
            {
                // Allow dragging the window / Разрешить перетаскивание окна
                this.DragMove();
                e.Handled = true;
            }
            return;
        }
        
        // Double-click to toggle fullscreen / Двойной клик для переключения полноэкранного режима
        if (e.ClickCount == 2)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        // Single click does nothing now / Одиночный клик теперь ничего не делает
    }

    private void VideoOverlay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isFullscreen) return;

        // Show control panel on any mouse movement in fullscreen mode
        // Показываем панель управления при любом движении мыши в полноэкранном режиме
        ShowControlPanel();
        StartControlPanelHideTimer();
    }

    private void VideoOverlay_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Change volume with mouse wheel / Изменение громкости колесом мыши
        // Delta > 0 = scroll up = increase volume / Delta > 0 = прокрутка вверх = увеличить громкость
        // Delta < 0 = scroll down = decrease volume / Delta < 0 = прокрутка вниз = уменьшить громкость
        double volumeChange = e.Delta > 0 ? 5 : -5;
        double newVolume = VolumeSlider.Value + volumeChange;
        
        // Clamp to valid range / Ограничить допустимым диапазоном
        newVolume = Math.Max(0, Math.Min(200, newVolume));
        VolumeSlider.Value = newVolume;
        
        e.Handled = true;
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
        WindowState = System.Windows.WindowState.Minimized;
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
        MenuAlwaysOnTop.IsChecked = true;
    }

    private void PinButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _settings.AlwaysOnTop = false;
        WindowHelper.SetAlwaysOnTop(this, false);
        MenuAlwaysOnTop.IsChecked = false;
    }

    #endregion

    #region Menu Events / События меню

    private void MenuOpenFile_Click(object sender, RoutedEventArgs e)
    {
        OpenVideoFile();
    }

    private void MenuStop_Click(object sender, RoutedEventArgs e)
    {
        _mediaPlayer?.Stop();
        PlayPauseButton.Content = "\uE768"; // Play icon
    }

    private void MenuVolumeUp_Click(object sender, RoutedEventArgs e)
    {
        ChangeVolume(5);
    }

    private void MenuVolumeDown_Click(object sender, RoutedEventArgs e)
    {
        ChangeVolume(-5);
    }

    private void MenuAlwaysOnTop_Checked(object sender, RoutedEventArgs e)
    {
        _settings.AlwaysOnTop = true;
        WindowHelper.SetAlwaysOnTop(this, true);
        PinButton.IsChecked = true;
    }

    private void MenuAlwaysOnTop_Unchecked(object sender, RoutedEventArgs e)
    {
        _settings.AlwaysOnTop = false;
        WindowHelper.SetAlwaysOnTop(this, false);
        PinButton.IsChecked = false;
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            "LangV Player v1.0\n\n" +
            "Видеоплеер для изучения языков\n" +
            "Video player for language learning\n\n" +
            "© 2026 LangV Team",
            "О программе",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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
        SeekRelative(-10000); // -10 seconds in ms
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        SeekRelative(10000); // +10 seconds in ms
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void CompactModeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleCompactMode();
    }

    private void VolumeIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Toggle mute / Переключение mute
        ToggleMute();
        e.Handled = true;
    }

    private void ToggleMute()
    {
        if (_mediaPlayer == null) return;
        
        if (_isMuted)
        {
            // Unmute - restore previous volume / Включить звук - восстановить предыдущую громкость
            _isMuted = false;
            VolumeSlider.Value = _volumeBeforeMute;
            _mediaPlayer.Volume = (int)_volumeBeforeMute;
        }
        else
        {
            // Mute - save current volume and set to 0 / Выключить звук - сохранить текущую громкость и установить 0
            _volumeBeforeMute = VolumeSlider.Value > 0 ? VolumeSlider.Value : 100;
            _isMuted = true;
            _mediaPlayer.Volume = 0;
        }
        UpdateVolumeIcon();
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_mediaPlayer != null)
        {
            // If user changes volume manually, unmute / Если пользователь вручную меняет громкость, убрать mute
            if (_isMuted && e.NewValue > 0)
            {
                _isMuted = false;
            }
            _mediaPlayer.Volume = (int)e.NewValue;
            _settings.Volume = e.NewValue;
            UpdateVolumeIcon();
        }
        
        // Update volume percentage text / Обновить текст процента громкости
        if (VolumePercentText != null)
        {
            VolumePercentText.Text = $"{(int)e.NewValue}%";
        }
    }

    private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Seek when user interacts with slider (not when TimeChanged updates it)
        // Перемотка когда пользователь взаимодействует со слайдером (не когда TimeChanged обновляет)
        if (_isDraggingSlider && _mediaPlayer != null && _mediaPlayer.Length > 0)
        {
            // Update time display during drag / Обновить время во время перетаскивания
            CurrentTimeText.Text = FormatTime(TimeSpan.FromMilliseconds(e.NewValue));
        }
    }

    private void TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        // User started dragging the thumb / Пользователь начал перетаскивать бегунок
        _isDraggingSlider = true;
    }

    private void TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        // User finished dragging - perform seek / Пользователь закончил перетаскивание - выполнить перемотку
        _isDraggingSlider = false;
        
        if (_mediaPlayer != null && _mediaPlayer.Length > 0)
        {
            // Block TimeChanged updates until VLC reaches new position
            // Блокировать обновления TimeChanged пока VLC не достигнет новой позиции
            _pendingSeekPosition = (long)TimelineSlider.Value;
            
            // Use Position (0.0-1.0) for better MKV support
            // Использовать Position (0.0-1.0) для лучшей поддержки MKV
            float position = (float)(TimelineSlider.Value / TimelineSlider.Maximum);
            _mediaPlayer.Position = position;
        }
    }

    private void TimelineSlider_TrackMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Handle click-to-seek on the track (not on thumb)
        // Обработка клика по дорожке для перемотки (не на бегунке)
        if (_mediaPlayer == null || _mediaPlayer.Length <= 0)
            return;
        
        // Skip if dragging (thumb handles that) / Пропустить если перетаскиваем (бегунок обработает)
        if (_isDraggingSlider)
            return;
        
        // Check if click is on the thumb - if so, skip (thumb will handle it)
        // Проверяем клик на бегунке - если да, пропускаем (бегунок обработает)
        var slider = TimelineSlider;
        var thumb = FindVisualChild<System.Windows.Controls.Primitives.Thumb>(slider);
        
        if (thumb != null)
        {
            var clickPoint = e.GetPosition(thumb);
            if (clickPoint.X >= 0 && clickPoint.X <= thumb.ActualWidth &&
                clickPoint.Y >= 0 && clickPoint.Y <= thumb.ActualHeight)
            {
                return; // Click on thumb, let it handle / Клик на бегунке, пусть обрабатывает
            }
        }
        
        // Calculate position from click / Вычислить позицию из клика
        var point = e.GetPosition(slider);
        var ratio = point.X / slider.ActualWidth;
        ratio = Math.Max(0.0, Math.Min(1.0, ratio)); // Clamp to 0-1 / Ограничить 0-1
        var newTimeMs = ratio * _mediaPlayer.Length;
        
        // Block TimeChanged updates until VLC reaches new position
        // Блокировать обновления TimeChanged пока VLC не достигнет новой позиции
        _pendingSeekPosition = (long)newTimeMs;
        
        // Update UI / Обновить интерфейс
        TimelineSlider.Value = newTimeMs;
        CurrentTimeText.Text = FormatTime(TimeSpan.FromMilliseconds(newTimeMs));
        
        // Perform seek using Position (0.0-1.0) for better MKV/AVI support
        // Выполнить перемотку используя Position (0.0-1.0) для лучшей поддержки MKV/AVI
        _mediaPlayer.Position = (float)ratio;
    }

    /// <summary>
    /// Find a visual child of specified type / Найти визуальный дочерний элемент указанного типа
    /// </summary>
    private T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }

    #endregion

    #region Timeline Thumbnail Preview / Превью миниатюр на таймлайне

    /// <summary>
    /// Handle mouse move over timeline - show thumbnail preview with debounce
    /// Обработка движения мыши над таймлайном - показать превью миниатюры с debounce
    /// </summary>
    private void TimelineMouseTracker_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Skip if dragging thumb or no video / Пропустить если перетаскиваем бегунок или нет видео
        if (_isDraggingThumb || _mediaPlayer == null || _mediaPlayer.Length <= 0)
        {
            return;
        }
        
        // Calculate time from mouse position / Вычислить время из позиции мыши
        var point = e.GetPosition(TimelineSlider);
        var ratio = point.X / TimelineSlider.ActualWidth;
        ratio = Math.Max(0.0, Math.Min(1.0, ratio));
        var timeMs = (long)(ratio * _mediaPlayer.Length);
        
        // Update popup position (center popup on mouse X position)
        // Обновить позицию popup (центрировать popup по позиции X мыши)
        double popupWidth = _thumbnailService?.IsEnabled == true ? ThumbnailService.ThumbnailWidth + 8 : 50;
        ThumbnailPopup.HorizontalOffset = point.X - popupWidth / 2;
        
        // Update time text immediately / Обновить текст времени сразу
        ThumbnailTimeText.Text = FormatTime(TimeSpan.FromMilliseconds(timeMs));
        
        // Show popup / Показать popup
        ThumbnailPopup.IsOpen = true;
        
        // Store time for debounced thumbnail generation
        // Сохранить время для отложенной генерации миниатюры
        _lastThumbnailTimeMs = timeMs;
        
        // Restart debounce timer / Перезапустить таймер debounce
        _thumbnailDebounceTimer?.Stop();
        _thumbnailDebounceTimer?.Start();
    }

    /// <summary>
    /// Debounce timer tick - generate thumbnail after mouse stops moving
    /// Тик таймера debounce - генерировать миниатюру после остановки мыши
    /// </summary>
    private async void ThumbnailDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _thumbnailDebounceTimer?.Stop();
        
        if (_isClosing || !ThumbnailPopup.IsOpen || _lastThumbnailTimeMs < 0)
            return;
        
        if (_thumbnailService?.IsEnabled != true)
        {
            ThumbnailImageBorder.Visibility = Visibility.Collapsed;
            return;
        }
        
        // Cancel previous request and dispose old CTS / Отменить предыдущий запрос и освободить старый CTS
        var oldCts = _thumbnailCts;
        _thumbnailCts = new CancellationTokenSource();
        if (oldCts != null)
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }
        
        try
        {
            var thumbnail = await _thumbnailService.GetThumbnailAsync(_lastThumbnailTimeMs, _thumbnailCts.Token);
            
            if (thumbnail != null && ThumbnailPopup.IsOpen && !_isClosing)
            {
                ThumbnailImage.Source = thumbnail;
                ThumbnailImageBorder.Visibility = Visibility.Visible;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled / Ожидаемо при отмене
        }
        catch
        {
            // Ignore errors - stability first / Игнорировать ошибки - стабильность прежде всего
        }
    }

    /// <summary>
    /// Handle mouse leave timeline - hide thumbnail preview
    /// Обработка ухода мыши с таймлайна - скрыть превью миниатюры
    /// </summary>
    private void TimelineMouseTracker_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Stop debounce timer / Остановить таймер debounce
        _thumbnailDebounceTimer?.Stop();
        _lastThumbnailTimeMs = -1;
        
        // Cancel and dispose CTS / Отменить и освободить CTS
        var oldCts = _thumbnailCts;
        _thumbnailCts = null;
        if (oldCts != null)
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }
        
        // Hide popup / Скрыть popup
        ThumbnailPopup.IsOpen = false;
        ThumbnailImage.Source = null;
        ThumbnailImageBorder.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Handle mouse down on timeline overlay - start drag or seek
    /// Обработка нажатия мыши на таймлайне - начать drag или seek
    /// </summary>
    private void TimelineMouseTracker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_mediaPlayer == null || _mediaPlayer.Length <= 0)
            return;
        
        // Check if click is on thumb / Проверить клик на бегунке
        var thumb = FindVisualChild<System.Windows.Controls.Primitives.Thumb>(TimelineSlider);
        if (thumb != null)
        {
            var thumbPos = e.GetPosition(thumb);
            if (thumbPos.X >= 0 && thumbPos.X <= thumb.ActualWidth &&
                thumbPos.Y >= 0 && thumbPos.Y <= thumb.ActualHeight)
            {
                // Click on thumb - start dragging / Клик на бегунке - начать перетаскивание
                _isDraggingThumb = true;
                _isDraggingSlider = true;
                ThumbnailPopup.IsOpen = false;
                
                // Capture mouse for drag / Захватить мышь для drag
                var tracker = sender as System.Windows.UIElement;
                tracker?.CaptureMouse();
                return;
            }
        }
        
        // Click on track - seek to position / Клик на дорожке - перемотка
        PerformSeekFromMousePosition(e.GetPosition(TimelineSlider));
    }

    /// <summary>
    /// Handle mouse up on timeline overlay - end drag
    /// Обработка отпускания мыши на таймлайне - завершить drag
    /// </summary>
    private void TimelineMouseTracker_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingThumb)
        {
            _isDraggingThumb = false;
            _isDraggingSlider = false;
            
            // Release mouse capture / Освободить захват мыши
            var tracker = sender as System.Windows.UIElement;
            tracker?.ReleaseMouseCapture();
            
            // Perform final seek / Выполнить финальную перемотку
            PerformSeekFromMousePosition(e.GetPosition(TimelineSlider));
        }
    }

    /// <summary>
    /// Perform seek from mouse position / Выполнить перемотку по позиции мыши
    /// </summary>
    private void PerformSeekFromMousePosition(System.Windows.Point point)
    {
        if (_mediaPlayer == null || _mediaPlayer.Length <= 0)
            return;
        
        var ratio = point.X / TimelineSlider.ActualWidth;
        ratio = Math.Max(0.0, Math.Min(1.0, ratio));
        var newTimeMs = ratio * _mediaPlayer.Length;
        
        // Block TimeChanged updates / Блокировать обновления TimeChanged
        _pendingSeekPosition = (long)newTimeMs;
        
        // Update UI / Обновить интерфейс
        TimelineSlider.Value = newTimeMs;
        CurrentTimeText.Text = FormatTime(TimeSpan.FromMilliseconds(newTimeMs));
        
        // Perform seek / Выполнить перемотку
        _mediaPlayer.Position = (float)ratio;
        
        // Return focus to window / Вернуть фокус на окно
        this.Focus();
    }

    #endregion

    #region VLC Player Methods / Методы VLC плеера

    private void InitializeVlcPlayer()
    {
        try
        {
            // Path to system VLC installation / Путь к системной установке VLC
            string vlcPath = @"C:\Program Files\VideoLAN\VLC";
            
            // Initialize LibVLC with system VLC path / Инициализация LibVLC с путем к системному VLC
            Core.Initialize(vlcPath);
            
            // Create LibVLC using system VLC (full codec support including AC3) /
            // Создание LibVLC с использованием системного VLC (полная поддержка кодеков включая AC3)
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);
            
            // Set the video output to our VideoView / Устанавливаем вывод видео в наш VideoView
            VideoView.MediaPlayer = _mediaPlayer;
            
            // Set initial volume / Установка начальной громкости
            VolumeSlider.Value = _settings.Volume;
            UpdateVolumeIcon();

            // Event handlers / Обработчики событий
            _mediaPlayer.Playing += MediaPlayer_Playing;
            _mediaPlayer.Paused += MediaPlayer_Paused;
            _mediaPlayer.Stopped += MediaPlayer_Stopped;
            _mediaPlayer.EndReached += MediaPlayer_EndReached;
            _mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
            _mediaPlayer.TimeChanged += MediaPlayer_TimeChanged; // Use TimeChanged event for position updates / Используем TimeChanged для обновления позиции
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to initialize VLC player.\nОшибка инициализации VLC плеера.\n\n{ex.Message}",
                "Error / Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void MediaPlayer_Playing(object? sender, EventArgs e)
    {
        if (_isClosing) return;
        
        Dispatcher.BeginInvoke(async () =>
        {
            if (_isClosing) return;
            
            PlayPauseButton.Content = "\uE769"; // Pause icon
            NoVideoPlaceholder.Visibility = Visibility.Collapsed;
            
            // Start subtitle timer / Запустить таймер субтитров
            _subtitleTimer?.Start();
            
            // Small delay to ensure media is fully loaded / Небольшая задержка для полной загрузки медиа
            await System.Threading.Tasks.Task.Delay(200);
            
            if (_isClosing || _mediaPlayer == null) return;
            
            // Update embedded subtitles menu / Обновить меню встроенных субтитров
            UpdateEmbeddedSubtitlesMenu();
            
            // Always disable VLC built-in subtitles - we use our own overlay
            // Всегда отключаем встроенные субтитры VLC - используем свой overlay
            _mediaPlayer.SetSpu(-1);
            
            // Ensure audio track is selected / Убедиться что аудиодорожка выбрана
            var audioTrackCount = _mediaPlayer.AudioTrackCount;
            if (audioTrackCount > 0 && _mediaPlayer.AudioTrack == -1)
            {
                // Select first audio track if none selected / Выбрать первую аудиодорожку если ни одна не выбрана
                _mediaPlayer.SetAudioTrack(1);
                System.Diagnostics.Debug.WriteLine($"Audio track auto-selected. Total tracks: {audioTrackCount}");
            }
            
            // Restore volume after media starts playing / Восстановить громкость после начала воспроизведения
            int targetVolume = _isMuted ? 0 : (int)VolumeSlider.Value;
            _mediaPlayer.Volume = targetVolume;
            
            // Debug: Log audio info / Отладка: Логирование информации об аудио
            System.Diagnostics.Debug.WriteLine($"Volume: {targetVolume}, AudioTrack: {_mediaPlayer.AudioTrack}, AudioTrackCount: {audioTrackCount}");
        });
    }

    private void MediaPlayer_Paused(object? sender, EventArgs e)
    {
        if (_isClosing) return;
        
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing) return;
            
            PlayPauseButton.Content = "\uE768"; // Play icon
            
            // Stop subtitle timer when paused / Остановить таймер субтитров при паузе
            _subtitleTimer?.Stop();
            
            // Show control panel when paused in fullscreen / Показываем панель управления при паузе в полноэкранном режиме
            if (_isFullscreen)
            {
                ShowControlPanel();
            }
        });
    }

    private void MediaPlayer_Stopped(object? sender, EventArgs e)
    {
        if (_isClosing) return;
        
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing) return;
            
            PlayPauseButton.Content = "\uE768"; // Play icon
            
            // Stop subtitle timer / Остановить таймер субтитров
            _subtitleTimer?.Stop();
            
            // Show control panel when stopped in fullscreen / Показываем панель управления при остановке в полноэкранном режиме
            if (_isFullscreen)
            {
                ShowControlPanel();
            }
        });
    }

    private void MediaPlayer_EndReached(object? sender, EventArgs e)
    {
        if (_isClosing) return;
        
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing) return;
            
            PlayPauseButton.Content = "\uE768"; // Play icon
            TimelineSlider.Value = 0;
            CurrentTimeText.Text = "00:00";
            
            // Stop subtitle timer / Остановить таймер субтитров
            _subtitleTimer?.Stop();
            // Hide subtitles / Скрыть субтитры
            PrimarySubtitleBorder.Visibility = Visibility.Collapsed;
            SecondarySubtitleBorder.Visibility = Visibility.Collapsed;
            
            // Show control panel when video ends in fullscreen / Показываем панель управления при завершении видео в полноэкранном режиме
            if (_isFullscreen)
            {
                ShowControlPanel();
            }
            
            // Auto-play next item in playlist / Автовоспроизведение следующего элемента плейлиста
            if (_currentPlaylistIndex >= 0 && _currentPlaylistIndex < _playlist.Count - 1)
            {
                PlayNextInPlaylist();
            }
        });
    }

    private void MediaPlayer_LengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
    {
        if (_isClosing) return;
        
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing) return;
            
            TimelineSlider.Maximum = e.Length;
            TotalTimeText.Text = FormatTime(TimeSpan.FromMilliseconds(e.Length));
            
            // Update thumbnail service with current video info
            // Обновить сервис миниатюр информацией о текущем видео
            _thumbnailService?.SetVideo(_currentVideoPath, e.Length);
        });
    }

    private void MediaPlayer_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        // TimeChanged is called from VLC when playback position changes
        // TimeChanged вызывается VLC когда позиция воспроизведения меняется
        if (_isClosing) return;
        
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing) return;
            
            // Skip update if user is dragging slider
            // Пропустить обновление если пользователь перетаскивает слайдер
            if (_isDraggingSlider)
                return;
            
            // Check if we have a pending seek position
            // Проверяем есть ли ожидающая позиция перемотки
            if (_pendingSeekPosition >= 0)
            {
                // Allow update only if VLC has reached near the target position (within 2 seconds)
                // Разрешить обновление только если VLC достиг целевой позиции (в пределах 2 секунд)
                if (Math.Abs(e.Time - _pendingSeekPosition) < 2000)
                {
                    _pendingSeekPosition = -1; // Clear pending seek / Сбросить ожидающую перемотку
                }
                else
                {
                    return; // Still waiting for VLC to reach target / Ещё ждём пока VLC достигнет цели
                }
            }
            
            TimelineSlider.Value = e.Time;
            CurrentTimeText.Text = FormatTime(TimeSpan.FromMilliseconds(e.Time));
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
            // Add to playlist if not already there / Добавить в плейлист если ещё нет
            AddToPlaylistAndPlay(dialog.FileName);
        }
    }

    /// <summary>
    /// Adds file to playlist and plays it / Добавляет файл в плейлист и воспроизводит его
    /// </summary>
    private void AddToPlaylistAndPlay(string filePath)
    {
        // Check if file is already in playlist / Проверить есть ли файл уже в плейлисте
        var existingItem = _playlist.FirstOrDefault(p => p.FilePath == filePath);
        int index;
        
        if (existingItem != null)
        {
            // File exists, just play it / Файл существует, просто воспроизвести
            index = _playlist.IndexOf(existingItem);
        }
        else
        {
            // Add new item to playlist / Добавить новый элемент в плейлист
            var newItem = new PlaylistItem { FilePath = filePath };
            _playlist.Add(newItem);
            index = _playlist.Count - 1;
        }
        
        // Play the item / Воспроизвести элемент
        PlayPlaylistItem(index);
    }

    private void LoadVideo(string filePath)
    {
        try
        {
            if (_libVLC == null || _mediaPlayer == null) return;

            // Stop current playback and dispose old media / Остановить текущее воспроизведение и освободить старый media
            _mediaPlayer.Stop();
            _currentMedia?.Dispose();
            
            // Reset pending seek position / Сбросить ожидающую позицию перемотки
            _pendingSeekPosition = -1;
            
            // Reset speed to normal / Сбросить скорость к нормальной
            SetSpeed(1.0f);
            
            // Clear current subtitles display / Очистить отображение текущих субтитров
            _currentPrimarySubtitle = null;
            _currentSecondarySubtitle = null;
            PrimarySubtitleBorder.Visibility = Visibility.Collapsed;
            SecondarySubtitleBorder.Visibility = Visibility.Collapsed;
            
            _currentVideoPath = filePath;
            _currentMedia = new Media(_libVLC, new Uri(filePath));
            _mediaPlayer.Play(_currentMedia);
            _settings.LastVideoPath = filePath;
            
            // Try to auto-load subtitles with same name / Попытаться автозагрузить субтитры с тем же именем
            TryAutoLoadSubtitles(filePath);
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
        if (_mediaPlayer == null) return;

        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
        }
        else
        {
            // If video ended, reload and play from start / Если видео закончилось, перезагружаем и воспроизводим с начала
            if (_mediaPlayer.State == VLCState.Ended && !string.IsNullOrEmpty(_currentVideoPath))
            {
                LoadVideo(_currentVideoPath);
            }
            else
            {
                _mediaPlayer.Play();
            }
            
            // Hide control panel when playing in fullscreen / Скрываем панель управления при воспроизведении в полноэкранном режиме
            if (_isFullscreen)
            {
                StartControlPanelHideTimer();
            }
        }
    }

    private void SeekRelative(long milliseconds)
    {
        if (_mediaPlayer == null || _mediaPlayer.Length == 0) return;

        var newTime = _mediaPlayer.Time + milliseconds;
        newTime = Math.Max(0, Math.Min(newTime, _mediaPlayer.Length));
        
        // Block TimeChanged updates / Блокировать обновления TimeChanged
        _pendingSeekPosition = newTime;
        
        // Use Position for better MKV support / Использовать Position для лучшей поддержки MKV
        float position = (float)newTime / _mediaPlayer.Length;
        _mediaPlayer.Position = position;
    }

    private void ChangeVolume(int delta)
    {
        // VLC supports 0-200% volume / VLC поддерживает громкость 0-200%
        var newVolume = Math.Max(0, Math.Min(200, VolumeSlider.Value + delta));
        VolumeSlider.Value = newVolume;
    }

    #endregion

    #region Speed Control / Управление скоростью

    /// <summary>
    /// Change playback speed by delta (e.g., +0.25 or -0.25)
    /// Изменить скорость воспроизведения на delta
    /// </summary>
    private void ChangeSpeed(float delta)
    {
        float newSpeed = _currentSpeed + delta;
        // Clamp to 0.25 - 2.0 range / Ограничить диапазон 0.25 - 2.0
        newSpeed = Math.Max(0.25f, Math.Min(2.0f, newSpeed));
        SetSpeed(newSpeed);
    }

    /// <summary>
    /// Set exact playback speed
    /// Установить точную скорость воспроизведения
    /// </summary>
    private void SetSpeed(float speed)
    {
        _currentSpeed = speed;
        
        // Apply speed to VLC player / Применить скорость к VLC плееру
        if (_mediaPlayer != null)
        {
            _mediaPlayer.SetRate(_currentSpeed);
        }
        
        // Update UI / Обновить UI
        UpdateSpeedDisplay();
    }

    /// <summary>
    /// Update speed display text
    /// Обновить отображение скорости
    /// </summary>
    private void UpdateSpeedDisplay()
    {
        SpeedText.Text = $"{_currentSpeed:F2}x".Replace(",", ".");
        
        // Highlight if not normal speed / Подсветить если скорость не нормальная
        if (Math.Abs(_currentSpeed - 1.0f) < 0.01f)
        {
            SpeedText.Foreground = (System.Windows.Media.Brush)FindResource("TextBrush");
        }
        else
        {
            SpeedText.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
        }
    }

    // Speed control button handlers / Обработчики кнопок скорости
    private void SpeedDown_Click(object sender, RoutedEventArgs e)
    {
        ChangeSpeed(-0.25f);
    }

    private void SpeedUp_Click(object sender, RoutedEventArgs e)
    {
        ChangeSpeed(0.25f);
    }

    private void SpeedText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Reset to normal speed on click / Сброс к нормальной скорости по клику
        SetSpeed(1.0f);
    }

    private void SpeedReset_Click(object sender, RoutedEventArgs e)
    {
        SetSpeed(1.0f);
    }

    private void SpeedPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.Tag is string tagValue)
        {
            if (float.TryParse(tagValue, System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out float speed))
            {
                SetSpeed(speed);
            }
        }
    }

    #endregion

    #region UI Helper Methods / Вспомогательные методы UI

    private void InitializeControlPanelTimer()
    {
        _controlPanelTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _controlPanelTimer.Tick += ControlPanelTimer_Tick;
    }

    private void ControlPanelTimer_Tick(object? sender, EventArgs e)
    {
        _controlPanelTimer?.Stop();
        
        // Hide control panel only if in fullscreen and playing / Скрываем панель только в полноэкранном режиме и при воспроизведении
        if (_isFullscreen && _mediaPlayer != null && _mediaPlayer.IsPlaying)
        {
            ControlPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void StartControlPanelHideTimer()
    {
        _controlPanelTimer?.Stop();
        _controlPanelTimer?.Start();
    }

    private void ShowControlPanel()
    {
        ControlPanel.Visibility = Visibility.Visible;
    }

    private void UpdateVolumeIcon()
    {
        if (_isMuted || VolumeSlider.Value == 0)
            VolumeIcon.Text = "\uE74F"; // Muted
        else if (VolumeSlider.Value < 50)
            VolumeIcon.Text = "\uE993"; // Low (0-49%)
        else if (VolumeSlider.Value < 100)
            VolumeIcon.Text = "\uE994"; // Medium (50-99%)
        else
            VolumeIcon.Text = "\uE767"; // High (100%+)
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.Hours > 0
            ? $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes:D2}:{time.Seconds:D2}";
    }

    private void ToggleMaximize()
    {
        if (WindowState == System.Windows.WindowState.Maximized)
        {
            WindowState = System.Windows.WindowState.Normal;
            MaximizeButton.Content = "\uE922"; // Maximize icon
        }
        else
        {
            WindowState = System.Windows.WindowState.Maximized;
            MaximizeButton.Content = "\uE923"; // Restore icon
        }
    }

    private void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            // Exit fullscreen / Выход из полноэкранного режима
            Topmost = PinButton.IsChecked == true; // Restore previous Topmost state / Восстановить предыдущее состояние Topmost
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
            
            // Get screen bounds for true fullscreen / Получить границы экрана для настоящего полноэкранного режима
            var screen = System.Windows.Forms.Screen.FromHandle(
                new System.Windows.Interop.WindowInteropHelper(this).Handle);
            var bounds = screen.Bounds;
            
            // Set window state to Normal first to allow manual sizing / Сначала Normal для ручного размера
            WindowState = System.Windows.WindowState.Normal;
            
            // Position window to cover entire screen including taskbar and widgets / 
            // Позиционировать окно на весь экран включая панель задач и виджеты
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            
            Topmost = true; // Always on top in fullscreen / Всегда поверх в полноэкранном режиме
            ControlPanel.Visibility = Visibility.Collapsed;
            FullscreenButton.Content = "\uE73F";
            _isFullscreen = true;
            
            // Focus window to ensure hotkeys work / Фокус на окно для работы горячих клавиш
            this.Focus();
        }
    }

    /// <summary>
    /// Toggle compact/PiP mode - small always-on-top window on all virtual desktops.
    /// Переключение компактного/PiP режима - маленькое окно поверх всех на всех виртуальных столах.
    /// </summary>
    private void ToggleCompactMode()
    {
        if (_isCompactMode)
        {
            // Exit compact mode / Выход из компактного режима
            ExitCompactMode();
        }
        else
        {
            // Exit fullscreen first if active / Сначала выйти из полноэкранного режима если активен
            if (_isFullscreen)
            {
                ToggleFullscreen();
            }
            
            // Enter compact mode / Вход в компактный режим
            EnterCompactMode();
        }
    }

    /// <summary>
    /// Enter compact/PiP mode.
    /// Войти в компактный режим.
    /// </summary>
    private void EnterCompactMode()
    {
        // Save current window state / Сохранить текущее состояние окна
        _compactPreviousWidth = Width;
        _compactPreviousHeight = Height;
        _compactPreviousLeft = Left;
        _compactPreviousTop = Top;
        _compactPreviousWindowState = WindowState;
        _compactPreviousPinState = PinButton.IsChecked == true;
        
        // Set window state to Normal for manual sizing / Установить Normal для ручного размера
        WindowState = System.Windows.WindowState.Normal;
        
        // Hide UI elements FIRST before sizing / Скрыть элементы UI СНАЧАЛА до изменения размера
        TitleBarRow.Height = new GridLength(0); // Collapse row height / Свернуть высоту строки
        TitleBar.Visibility = Visibility.Collapsed;
        ControlPanel.Visibility = Visibility.Collapsed;
        PlaylistPanel.Visibility = Visibility.Collapsed;
        
        // Set minimum size for compact mode / Установить минимальный размер для компактного режима
        MinWidth = 200;
        MinHeight = 112;
        
        // Set compact size / Установить компактный размер
        Width = _compactWidth;
        Height = _compactHeight;
        
        // Position in bottom-right corner of screen with DPI scaling / 
        // Позиция в правом нижнем углу экрана с учётом DPI
        var screen = System.Windows.Forms.Screen.FromHandle(
            new System.Windows.Interop.WindowInteropHelper(this).Handle);
        var workingArea = screen.WorkingArea;
        
        // Convert screen pixels to WPF units / Конвертировать пиксели экрана в единицы WPF
        WindowHelper.GetDpiScale(this, out double scaleX, out double scaleY);
        double wpfRight = workingArea.Right / scaleX;
        double wpfBottom = workingArea.Bottom / scaleY;
        
        Left = wpfRight - _compactWidth - 20; // 20px margin / 20px отступ
        Top = wpfBottom - _compactHeight - 20;
        
        // Always on top / Всегда поверх
        Topmost = true;
        PinButton.IsChecked = true;
        
        // Pin to all virtual desktops (makes window show on all desktops) / 
        // Закрепить на всех виртуальных рабочих столах
        WindowHelper.SetShowOnAllVirtualDesktops(this, true);
        
        // Update button icon if exists / Обновить иконку кнопки если существует
        if (CompactModeButton != null)
        {
            CompactModeButton.Content = "\uEE49"; // Exit PiP icon
            CompactModeButton.ToolTip = "Выйти из компактного режима (P)";
        }
        
        _isCompactMode = true;

        // Add padding to specific area to allow resize handles to work (window chrome needs access to edges)
        // Добавить отсуп к области видео, чтобы работали ручки изменения размера (window chrome нужен доступ к краям)
        if (VideoArea != null)
        {
            VideoArea.Margin = new Thickness(5);
        }

        // Update subtitle size immediately / Обновить размер субтитров немедленно
        UpdateSubtitleFontSize();

        // Force focus on window for hotkeys / Принудительный фокус для горячих клавиш
        this.Activate();
        this.Focus();
    }

    /// <summary>
    /// Exit compact/PiP mode.
    /// Выйти из компактного режима.
    /// </summary>
    private void ExitCompactMode()
    {
        // Unpin from all virtual desktops / Открепить от всех виртуальных рабочих столов
        WindowHelper.SetShowOnAllVirtualDesktops(this, false);
        
        // Restore window state / Восстановить состояние окна
        WindowState = _compactPreviousWindowState;
        Width = _compactPreviousWidth;
        Height = _compactPreviousHeight;
        Left = _compactPreviousLeft;
        Top = _compactPreviousTop;
        
        // Show UI elements / Показать элементы UI
        TitleBarRow.Height = new GridLength(30); // Restore row height / Восстановить высоту строки
        TitleBar.Visibility = Visibility.Visible;
        ControlPanel.Visibility = Visibility.Visible;
        // Playlist panel visibility is controlled by toggle / Видимость плейлиста контролируется переключателем
        
        // Restore minimum size / Восстановить минимальный размер
        MinWidth = 600;
        MinHeight = 400;
        
        // Restore Topmost state / Восстановить состояние Topmost
        PinButton.IsChecked = _compactPreviousPinState;
        Topmost = _compactPreviousPinState;
        WindowHelper.SetAlwaysOnTop(this, _compactPreviousPinState);
        
        // Update button icon if exists / Обновить иконку кнопки если существует
        if (CompactModeButton != null)
        {
            CompactModeButton.Content = "\uEE47"; // PiP icon
            CompactModeButton.ToolTip = "Компактный режим (P)";
        }
        
        _isCompactMode = false;

        // Restore video area margin / Восстановить отступ области видео
        if (VideoArea != null)
        {
            VideoArea.Margin = new Thickness(0);
        }

        // Update subtitle size immediately / Обновить размер субтитров немедленно
        UpdateSubtitleFontSize();

        // Focus window / Фокус на окно
        this.Focus();
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
            WindowState = System.Windows.WindowState.Maximized;
        }
    }

    private void SaveCurrentSettings()
    {
        // Save window state / Сохранение состояния окна
        _settings.IsMaximized = WindowState == System.Windows.WindowState.Maximized;

        if (WindowState == System.Windows.WindowState.Normal)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
        }

        SettingsService.Save(_settings);
    }

    #endregion

    #region Playlist Methods / Методы плейлиста

    private void PlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        // Toggle playlist panel visibility / Переключить видимость панели плейлиста
        if (PlaylistPanel.Visibility == Visibility.Visible)
        {
            PlaylistPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            PlaylistPanel.Visibility = Visibility.Visible;
        }
    }

    private void PlaylistAddFiles_Click(object sender, RoutedEventArgs e)
    {
        // Open file dialog to add files to playlist / Открыть диалог для добавления файлов в плейлист
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add to Playlist / Добавить в плейлист",
            Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                // Check if file is already in playlist / Проверить, есть ли файл уже в плейлисте
                if (!_playlist.Any(p => p.FilePath == file))
                {
                    _playlist.Add(new PlaylistItem { FilePath = file });
                }
            }

            // If this is the first file and nothing is playing, start playback
            // Если это первый файл и ничего не воспроизводится, начать воспроизведение
            if (_playlist.Count > 0 && string.IsNullOrEmpty(_currentVideoPath))
            {
                PlayPlaylistItem(0);
            }
        }
    }

    private void PlaylistClear_Click(object sender, RoutedEventArgs e)
    {
        // Clear playlist / Очистить плейлист
        _playlist.Clear();
        _currentPlaylistIndex = -1;
    }

    private void PlaylistListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Play selected item on double-click / Воспроизвести выбранный элемент по двойному клику
        if (PlaylistListBox.SelectedItem is PlaylistItem item)
        {
            int index = _playlist.IndexOf(item);
            if (index >= 0)
            {
                PlayPlaylistItem(index);
            }
        }
    }

    private void PlayPlaylistItem(int index)
    {
        // Play item at specified index / Воспроизвести элемент по указанному индексу
        if (index < 0 || index >= _playlist.Count)
            return;

        // Update playing state for all items / Обновить состояние воспроизведения для всех элементов
        for (int i = 0; i < _playlist.Count; i++)
        {
            _playlist[i].IsPlaying = (i == index);
        }

        _currentPlaylistIndex = index;
        var item = _playlist[index];
        LoadVideo(item.FilePath);
        
        // Select the item in the list / Выбрать элемент в списке
        PlaylistListBox.SelectedIndex = index;
    }

    private void PlayNextInPlaylist()
    {
        // Play next item in playlist / Воспроизвести следующий элемент плейлиста
        if (_currentPlaylistIndex < _playlist.Count - 1)
        {
            PlayPlaylistItem(_currentPlaylistIndex + 1);
        }
    }

    private void PlayPreviousInPlaylist()
    {
        // Play previous item in playlist / Воспроизвести предыдущий элемент плейлиста
        if (_currentPlaylistIndex > 0)
        {
            PlayPlaylistItem(_currentPlaylistIndex - 1);
        }
    }

    #endregion

    #region Subtitle Methods / Методы субтитров

    /// <summary>
    /// Timer tick handler for subtitle synchronization.
    /// Обработчик тика таймера для синхронизации субтитров.
    /// </summary>
    private void SubtitleTimer_Tick(object? sender, EventArgs e)
    {
        if (_isClosing || _mediaPlayer == null)
            return;

        long currentTimeMs = _mediaPlayer.Time;
        
        // Update primary subtitles / Обновить основные субтитры
        if (_showPrimarySubtitles && _primarySubtitles.Count > 0)
        {
            var subtitle = Services.SrtParserService.FindActiveSubtitle(_primarySubtitles, currentTimeMs);
            if (subtitle != _currentPrimarySubtitle)
            {
                _currentPrimarySubtitle = subtitle;
                UpdatePrimarySubtitleDisplay(subtitle);
            }
        }
        
        // Update secondary subtitles / Обновить вторые субтитры
        if (_showSecondarySubtitles && _secondarySubtitles.Count > 0)
        {
            var subtitle = Services.SrtParserService.FindActiveSubtitle(_secondarySubtitles, currentTimeMs);
            if (subtitle != _currentSecondarySubtitle)
            {
                _currentSecondarySubtitle = subtitle;
                UpdateSecondarySubtitleDisplay(subtitle);
            }
        }
    }

    /// <summary>
    /// Update primary subtitle display.
    /// Обновить отображение основных субтитров.
    /// </summary>
    private void UpdatePrimarySubtitleDisplay(Models.SubtitleItem? subtitle)
    {
        if (subtitle != null)
        {
            PrimarySubtitleText.Text = subtitle.Text;
            PrimarySubtitleBorder.Visibility = Visibility.Visible;
        }
        else
        {
            PrimarySubtitleBorder.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Update secondary subtitle display.
    /// Обновить отображение вторых субтитров.
    /// </summary>
    private void UpdateSecondarySubtitleDisplay(Models.SubtitleItem? subtitle)
    {
        if (subtitle != null)
        {
            SecondarySubtitleText.Text = subtitle.Text;
            SecondarySubtitleBorder.Visibility = Visibility.Visible;
        }
        else
        {
            SecondarySubtitleBorder.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Load primary subtitle file.
    /// Загрузить файл основных субтитров.
    /// </summary>
    private void LoadPrimarySubtitles(string filePath)
    {
        _primarySubtitles = Services.SrtParserService.Parse(filePath);
        _currentPrimarySubtitle = null;
        
        // Update menu label / Обновить метку меню
        string fileName = System.IO.Path.GetFileName(filePath);
        _primarySubtitleSource = fileName;
        MenuTogglePrimarySubs.Header = $"Субтитры 1: {fileName}";
        MenuTogglePrimarySubs.IsChecked = true;
        _showPrimarySubtitles = true;
        
        // Uncheck embedded menu items / Снять галочки с элементов встроенных субтитров
        foreach (var item in MenuEmbeddedSubs1.Items)
        {
            if (item is System.Windows.Controls.MenuItem mi && mi.IsCheckable)
            {
                mi.IsChecked = mi == MenuEmbeddedSubs1None;
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"Loaded {_primarySubtitles.Count} primary subtitles from {fileName}");
    }

    /// <summary>
    /// Load secondary subtitle file.
    /// Загрузить файл вторых субтитров.
    /// </summary>
    private void LoadSecondarySubtitles(string filePath)
    {
        _secondarySubtitles = Services.SrtParserService.Parse(filePath);
        _currentSecondarySubtitle = null;
        
        // Update menu label / Обновить метку меню
        string fileName = System.IO.Path.GetFileName(filePath);
        _secondarySubtitleSource = fileName;
        MenuToggleSecondarySubs.Header = $"Субтитры 2: {fileName}";
        MenuToggleSecondarySubs.IsChecked = true;
        _showSecondarySubtitles = true;
        
        // Uncheck embedded menu items / Снять галочки с элементов встроенных субтитров
        foreach (var item in MenuEmbeddedSubs2.Items)
        {
            if (item is System.Windows.Controls.MenuItem mi && mi.IsCheckable)
            {
                mi.IsChecked = mi == MenuEmbeddedSubs2None;
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"Loaded {_secondarySubtitles.Count} secondary subtitles from {fileName}");
    }

    /// <summary>
    /// Clear all subtitles.
    /// Очистить все субтитры.
    /// </summary>
    private void ClearAllSubtitles()
    {
        _primarySubtitles.Clear();
        _secondarySubtitles.Clear();
        _primarySubtitleSource = null;
        _secondarySubtitleSource = null;
        _currentPrimarySubtitle = null;
        _currentSecondarySubtitle = null;
        
        PrimarySubtitleBorder.Visibility = Visibility.Collapsed;
        SecondarySubtitleBorder.Visibility = Visibility.Collapsed;
        
        MenuTogglePrimarySubs.Header = "Субтитры 1: (нет)";
        MenuToggleSecondarySubs.Header = "Субтитры 2: (нет)";
        
        // Uncheck all embedded menu items / Снять галочки со всех элементов встроенных меню
        foreach (var item in MenuEmbeddedSubs1.Items)
        {
            if (item is System.Windows.Controls.MenuItem mi && mi.IsCheckable)
            {
                mi.IsChecked = mi == MenuEmbeddedSubs1None;
            }
        }
        foreach (var item in MenuEmbeddedSubs2.Items)
        {
            if (item is System.Windows.Controls.MenuItem mi && mi.IsCheckable)
            {
                mi.IsChecked = mi == MenuEmbeddedSubs2None;
            }
        }
    }

    /// <summary>
    /// Try to auto-load subtitles with same name as video file.
    /// Попытаться автоматически загрузить субтитры с тем же именем, что и видеофайл.
    /// </summary>
    private void TryAutoLoadSubtitles(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath))
            return;

        string directory = System.IO.Path.GetDirectoryName(videoPath) ?? "";
        string fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(videoPath);

        // Common subtitle file patterns / Распространённые паттерны файлов субтитров
        var patterns = new[]
        {
            $"{fileNameWithoutExt}.srt",
            $"{fileNameWithoutExt}.en.srt",
            $"{fileNameWithoutExt}.eng.srt",
            $"{fileNameWithoutExt}.ru.srt",
            $"{fileNameWithoutExt}.rus.srt",
        };

        bool primaryLoaded = false;
        
        foreach (var pattern in patterns)
        {
            string srtPath = System.IO.Path.Combine(directory, pattern);
            if (System.IO.File.Exists(srtPath))
            {
                if (!primaryLoaded)
                {
                    LoadPrimarySubtitles(srtPath);
                    primaryLoaded = true;
                }
                else
                {
                    LoadSecondarySubtitles(srtPath);
                    break; // Only load first two found / Загрузить только первые два найденных
                }
            }
        }
    }

    #region Subtitle Menu Handlers / Обработчики меню субтитров

    private void MenuLoadPrimarySubs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Subtitle files (*.srt)|*.srt|All files (*.*)|*.*",
            Title = "Выберите файл субтитров 1"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadPrimarySubtitles(dialog.FileName);
        }
    }

    private void MenuLoadSecondarySubs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Subtitle files (*.srt)|*.srt|All files (*.*)|*.*",
            Title = "Выберите файл субтитров 2"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadSecondarySubtitles(dialog.FileName);
        }
    }

    private void MenuTogglePrimarySubs_Changed(object sender, RoutedEventArgs e)
    {
        _showPrimarySubtitles = MenuTogglePrimarySubs.IsChecked;
        if (!_showPrimarySubtitles)
        {
            PrimarySubtitleBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void MenuToggleSecondarySubs_Changed(object sender, RoutedEventArgs e)
    {
        _showSecondarySubtitles = MenuToggleSecondarySubs.IsChecked;
        if (!_showSecondarySubtitles)
        {
            SecondarySubtitleBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void MenuClearAllSubs_Click(object sender, RoutedEventArgs e)
    {
        ClearAllSubtitles();
    }

    /// <summary>
    /// Updates the embedded subtitles menus with available tracks from video file.
    /// Обновляет меню встроенных субтитров доступными дорожками из видеофайла.
    /// </summary>
    private async void UpdateEmbeddedSubtitlesMenu()
    {
        // Clear dynamic items from both menus (keep only "Отключить" and separator)
        // Очистить динамические элементы из обоих меню
        ClearEmbeddedSubtitlesMenuItems(MenuEmbeddedSubs1);
        ClearEmbeddedSubtitlesMenuItems(MenuEmbeddedSubs2);
        
        // Get embedded subtitle tracks via FFmpeg / Получить дорожки субтитров через FFmpeg
        if (_subtitleExtractor?.IsAvailable == true && !string.IsNullOrEmpty(_currentVideoPath))
        {
            try
            {
                _embeddedSubtitleTracks = await _subtitleExtractor.GetSubtitleTracksAsync(_currentVideoPath);
                System.Diagnostics.Debug.WriteLine($"Found {_embeddedSubtitleTracks.Count} embedded subtitle tracks via FFmpeg");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting subtitle tracks: {ex.Message}");
                _embeddedSubtitleTracks.Clear();
            }
        }
        else
        {
            _embeddedSubtitleTracks.Clear();
        }
        
        if (_embeddedSubtitleTracks.Count == 0)
        {
            // No embedded subtitles / Нет встроенных субтитров
            AddNoSubtitlesMenuItem(MenuEmbeddedSubs1);
            AddNoSubtitlesMenuItem(MenuEmbeddedSubs2);
            return;
        }

        // Add tracks to both menus / Добавить дорожки в оба меню
        foreach (var track in _embeddedSubtitleTracks)
        {
            AddEmbeddedTrackMenuItem(MenuEmbeddedSubs1, track, 1);
            AddEmbeddedTrackMenuItem(MenuEmbeddedSubs2, track, 2);
        }
        
        // Disable built-in VLC subtitles - we use our own overlay
        // Отключить встроенные субтитры VLC - используем свой overlay
        _mediaPlayer?.SetSpu(-1);
    }
    
    /// <summary>
    /// Clears dynamic menu items from embedded subtitles menu.
    /// Очищает динамические элементы из меню встроенных субтитров.
    /// </summary>
    private void ClearEmbeddedSubtitlesMenuItems(System.Windows.Controls.MenuItem menu)
    {
        while (menu.Items.Count > 2)
        {
            menu.Items.RemoveAt(menu.Items.Count - 1);
        }
    }
    
    /// <summary>
    /// Adds "no subtitles" placeholder to menu.
    /// Добавляет заглушку "нет субтитров" в меню.
    /// </summary>
    private void AddNoSubtitlesMenuItem(System.Windows.Controls.MenuItem menu)
    {
        var noSubsItem = new System.Windows.Controls.MenuItem
        {
            Header = "(нет встроенных субтитров)",
            IsEnabled = false
        };
        menu.Items.Add(noSubsItem);
    }
    
    /// <summary>
    /// Adds embedded track menu item.
    /// Добавляет элемент меню встроенной дорожки.
    /// </summary>
    private void AddEmbeddedTrackMenuItem(
        System.Windows.Controls.MenuItem menu, 
        Services.SubtitleExtractorService.SubtitleTrackInfo track, 
        int slot)
    {
        // Check if this track is currently selected for this slot
        // Проверить, выбрана ли эта дорожка для этого слота
        int selectedTrack = slot == 1 ? _selectedEmbeddedTrackSlot1 : _selectedEmbeddedTrackSlot2;
        bool isChecked = track.StreamIndex == selectedTrack;
        
        var menuItem = new System.Windows.Controls.MenuItem
        {
            Header = track.DisplayName,
            Tag = new Tuple<int, int>(track.StreamIndex, slot), // StreamIndex and slot number
            IsCheckable = true,
            IsChecked = isChecked
        };
        
        menuItem.Click += EmbeddedSubtitleTrack_Click;
        menu.Items.Add(menuItem);
        
        // Update "Disable" checkbox state / Обновить состояние галочки "Отключить"
        var noneMenuItem = slot == 1 ? MenuEmbeddedSubs1None : MenuEmbeddedSubs2None;
        noneMenuItem.IsChecked = selectedTrack == -1;
    }

    /// <summary>
    /// Handles click on embedded subtitle track menu item.
    /// Обрабатывает клик по элементу меню встроенной дорожки субтитров.
    /// </summary>
    private async void EmbeddedSubtitleTrack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem menuItem) return;
        if (menuItem.Tag is not Tuple<int, int> trackInfo) return;
        
        int streamIndex = trackInfo.Item1;
        int slot = trackInfo.Item2;
        
        // Save selected track index / Сохранить выбранный индекс дорожки
        if (slot == 1)
            _selectedEmbeddedTrackSlot1 = streamIndex;
        else
            _selectedEmbeddedTrackSlot2 = streamIndex;
        
        var parentMenu = slot == 1 ? MenuEmbeddedSubs1 : MenuEmbeddedSubs2;
        var noneMenuItem = slot == 1 ? MenuEmbeddedSubs1None : MenuEmbeddedSubs2None;
        
        // Uncheck all other items in this menu / Снять галочки с других элементов в этом меню
        foreach (var item in parentMenu.Items)
        {
            if (item is System.Windows.Controls.MenuItem mi && mi.IsCheckable)
            {
                mi.IsChecked = false;
            }
        }
        
        menuItem.IsChecked = true;
        noneMenuItem.IsChecked = false;
        
        // Extract and load subtitles / Извлечь и загрузить субтитры
        if (_subtitleExtractor?.IsAvailable == true && !string.IsNullOrEmpty(_currentVideoPath))
        {
            try
            {
                var trackName = _embeddedSubtitleTracks.FirstOrDefault(t => t.StreamIndex == streamIndex)?.DisplayName ?? $"Track {streamIndex}";
                
                // Show loading indicator / Показать индикатор загрузки
                if (slot == 1)
                {
                    MenuTogglePrimarySubs.Header = $"Субтитры 1: загрузка...";
                }
                else
                {
                    MenuToggleSecondarySubs.Header = $"Субтитры 2: загрузка...";
                }
                
                var subtitles = await _subtitleExtractor.ExtractSubtitlesAsync(_currentVideoPath, streamIndex);
                
                if (subtitles.Count > 0)
                {
                    if (slot == 1)
                    {
                        _primarySubtitles = subtitles;
                        _primarySubtitleSource = $"[Встр.] {trackName}";
                        _currentPrimarySubtitle = null;
                        MenuTogglePrimarySubs.Header = $"Субтитры 1: {_primarySubtitleSource}";
                        MenuTogglePrimarySubs.IsChecked = true;
                        _showPrimarySubtitles = true;
                    }
                    else
                    {
                        _secondarySubtitles = subtitles;
                        _secondarySubtitleSource = $"[Встр.] {trackName}";
                        _currentSecondarySubtitle = null;
                        MenuToggleSecondarySubs.Header = $"Субтитры 2: {_secondarySubtitleSource}";
                        MenuToggleSecondarySubs.IsChecked = true;
                        _showSecondarySubtitles = true;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Loaded {subtitles.Count} embedded subtitles for slot {slot}");
                }
                else
                {
                    // Failed to extract / Не удалось извлечь
                    if (slot == 1)
                    {
                        MenuTogglePrimarySubs.Header = "Субтитры 1: (ошибка)";
                    }
                    else
                    {
                        MenuToggleSecondarySubs.Header = "Субтитры 2: (ошибка)";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting subtitles: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Handles click on "Disable" embedded subtitles 1 menu item.
    /// Обрабатывает клик на пункт "Отключить" встроенные субтитры 1.
    /// </summary>
    private void MenuEmbeddedSubs1None_Click(object sender, RoutedEventArgs e)
    {
        // Reset selected track index / Сбросить выбранный индекс дорожки
        _selectedEmbeddedTrackSlot1 = -1;
        
        // Uncheck all embedded subtitle items / Снять галочки со всех элементов встроенных субтитров
        foreach (var item in MenuEmbeddedSubs1.Items)
        {
            if (item is System.Windows.Controls.MenuItem mi && mi.IsCheckable && mi != MenuEmbeddedSubs1None)
            {
                mi.IsChecked = false;
            }
        }
        
        MenuEmbeddedSubs1None.IsChecked = true;
        
        // Clear primary subtitles if they were from embedded / Очистить первичные субтитры если они были встроенными
        if (_primarySubtitleSource?.StartsWith("[Встр.]") == true)
        {
            _primarySubtitles.Clear();
            _primarySubtitleSource = null;
            _currentPrimarySubtitle = null;
            PrimarySubtitleBorder.Visibility = Visibility.Collapsed;
            MenuTogglePrimarySubs.Header = "Субтитры 1: (нет)";
        }
        
        System.Diagnostics.Debug.WriteLine("Embedded subtitles 1 disabled");
    }
    
    /// <summary>
    /// Handles click on "Disable" embedded subtitles 2 menu item.
    /// Обрабатывает клик на пункт "Отключить" встроенные субтитры 2.
    /// </summary>
    private void MenuEmbeddedSubs2None_Click(object sender, RoutedEventArgs e)
    {
        // Reset selected track index / Сбросить выбранный индекс дорожки
        _selectedEmbeddedTrackSlot2 = -1;
        
        // Uncheck all embedded subtitle items / Снять галочки со всех элементов встроенных субтитров
        foreach (var item in MenuEmbeddedSubs2.Items)
        {
            if (item is System.Windows.Controls.MenuItem mi && mi.IsCheckable && mi != MenuEmbeddedSubs2None)
            {
                mi.IsChecked = false;
            }
        }
        
        MenuEmbeddedSubs2None.IsChecked = true;
        
        // Clear secondary subtitles if they were from embedded / Очистить вторичные субтитры если они были встроенными
        if (_secondarySubtitleSource?.StartsWith("[Встр.]") == true)
        {
            _secondarySubtitles.Clear();
            _secondarySubtitleSource = null;
            _currentSecondarySubtitle = null;
            SecondarySubtitleBorder.Visibility = Visibility.Collapsed;
            MenuToggleSecondarySubs.Header = "Субтитры 2: (нет)";
        }
        
        System.Diagnostics.Debug.WriteLine("Embedded subtitles 2 disabled");
    }

    #endregion

    #endregion
}
