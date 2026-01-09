using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace LangVPlayer.Services
{
    /// <summary>
    /// Service for generating video thumbnails using FFmpeg (primary) or graceful degradation.
    /// Сервис для генерации миниатюр видео используя FFmpeg (основной) или деградация.
    /// 
    /// STABILITY FIRST: If thumbnail generation fails, the player continues normally.
    /// СТАБИЛЬНОСТЬ ПРЕЖДЕ ВСЕГО: Если генерация миниатюр не работает, плеер продолжает нормально.
    /// </summary>
    public class ThumbnailService : IDisposable
    {
        #region Fields / Поля

        private readonly ConcurrentDictionary<long, BitmapImage> _thumbnailCache;
        private readonly string _tempFolder;
        private readonly SemaphoreSlim _generationSemaphore = new SemaphoreSlim(1, 1);
        
        private string? _currentVideoPath;
        private long _currentVideoDurationMs;
        private bool _isDisposed;
        private bool _isThumbnailsEnabled = true;
        private string? _ffmpegPath;
        
        // Thumbnail settings / Настройки миниатюр
        public const int ThumbnailWidth = 200;
        public const int ThumbnailHeight = 112;
        private const int CacheIntervalMs = 10000; // Cache every 10 seconds / Кэшировать каждые 10 секунд
        private const int MaxCacheSize = 60; // Maximum thumbnails in cache / Максимум миниатюр в кэше
        private const int GenerationTimeoutMs = 3000; // 3 second timeout / 3 секунды таймаут

        #endregion

        #region Properties / Свойства

        /// <summary>
        /// Whether thumbnail generation is enabled and working
        /// Включена ли и работает ли генерация миниатюр
        /// </summary>
        public bool IsEnabled => _isThumbnailsEnabled && _ffmpegPath != null;

        #endregion

        #region Constructor / Конструктор

        public ThumbnailService()
        {
            _thumbnailCache = new ConcurrentDictionary<long, BitmapImage>();
            _tempFolder = Path.Combine(Path.GetTempPath(), "LangVPlayer", "Thumbnails");
            
            // Create temp folder if not exists / Создать временную папку если не существует
            try
            {
                if (!Directory.Exists(_tempFolder))
                {
                    Directory.CreateDirectory(_tempFolder);
                }
            }
            catch
            {
                _tempFolder = Path.GetTempPath();
            }
            
            // Find FFmpeg / Найти FFmpeg
            FindFFmpeg();
        }

        #endregion

        #region Public Methods / Публичные методы

        /// <summary>
        /// Set current video for thumbnail generation
        /// Установить текущее видео для генерации миниатюр
        /// </summary>
        public void SetVideo(string? videoPath, long durationMs)
        {
            if (_currentVideoPath != videoPath)
            {
                _currentVideoPath = videoPath;
                _currentVideoDurationMs = durationMs;
                ClearCache();
            }
            else if (_currentVideoDurationMs != durationMs)
            {
                _currentVideoDurationMs = durationMs;
            }
        }

        /// <summary>
        /// Get thumbnail for specific time position (in milliseconds).
        /// Returns null quickly if not available - never blocks UI.
        /// Получить миниатюру для позиции (в миллисекундах).
        /// Возвращает null быстро если недоступно - никогда не блокирует UI.
        /// </summary>
        public async Task<BitmapImage?> GetThumbnailAsync(long timeMs, CancellationToken cancellationToken = default)
        {
            // Quick checks / Быстрые проверки
            if (!_isThumbnailsEnabled || _isDisposed || _ffmpegPath == null)
                return null;
                
            if (string.IsNullOrEmpty(_currentVideoPath) || !File.Exists(_currentVideoPath))
                return null;
                
            if (_currentVideoDurationMs <= 0)
                return null;

            // Round to nearest cache interval / Округлить до ближайшего интервала кэша
            long cacheKey = (timeMs / CacheIntervalMs) * CacheIntervalMs;

            // Check cache first / Сначала проверить кэш
            if (_thumbnailCache.TryGetValue(cacheKey, out var cachedThumbnail))
            {
                return cachedThumbnail;
            }

            // Try to acquire semaphore with short wait (50ms max)
            // Попытаться получить семафор с коротким ожиданием (максимум 50мс)
            if (!await _generationSemaphore.WaitAsync(50, cancellationToken))
            {
                // Still busy, return null / Всё ещё занят, вернуть null
                return null;
            }

            try
            {
                // Double-check cache / Двойная проверка кэша
                if (_thumbnailCache.TryGetValue(cacheKey, out cachedThumbnail))
                {
                    return cachedThumbnail;
                }

                if (cancellationToken.IsCancellationRequested)
                    return null;

                // Generate with timeout / Генерировать с таймаутом
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(GenerationTimeoutMs);
                
                var thumbnail = await Task.Run(() => GenerateThumbnailWithFFmpeg(cacheKey, cts.Token), cts.Token);
                
                if (thumbnail != null && !cancellationToken.IsCancellationRequested && _thumbnailCache.Count < MaxCacheSize)
                {
                    _thumbnailCache.TryAdd(cacheKey, thumbnail);
                }
                
                return thumbnail;
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation / Ожидаемо при отмене
                return null;
            }
            catch (Exception ex)
            {
                // Log and continue / Логировать и продолжить
                System.Diagnostics.Debug.WriteLine($"Thumbnail error: {ex.Message}");
                return null;
            }
            finally
            {
                _generationSemaphore.Release();
            }
        }

        /// <summary>
        /// Clear thumbnail cache
        /// Очистить кэш миниатюр
        /// </summary>
        public void ClearCache()
        {
            _thumbnailCache.Clear();
        }

        /// <summary>
        /// Enable or disable thumbnail generation
        /// Включить или отключить генерацию миниатюр
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isThumbnailsEnabled = enabled;
            if (!enabled)
            {
                ClearCache();
            }
        }

        #endregion

        #region Private Methods / Приватные методы

        /// <summary>
        /// Find FFmpeg in common locations
        /// Найти FFmpeg в стандартных местах
        /// </summary>
        private void FindFFmpeg()
        {
            // Check common locations (app folder first, then system)
            // Проверить стандартные места (сначала папка приложения, потом система)
            var possiblePaths = new[]
            {
                // App folder / Папка приложения (приоритет)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
                // System locations / Системные расположения
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin", "ffmpeg.exe"),
                "ffmpeg", // In PATH / В PATH
            };

            foreach (var path in possiblePaths)
            {
                if (TestFFmpeg(path))
                {
                    _ffmpegPath = path;
                    System.Diagnostics.Debug.WriteLine($"FFmpeg found: {path}");
                    return;
                }
            }
            
            // FFmpeg not found - thumbnails disabled / FFmpeg не найден - миниатюры отключены
            _isThumbnailsEnabled = false;
            System.Diagnostics.Debug.WriteLine("FFmpeg not found. Thumbnails disabled.");
        }

        /// <summary>
        /// Test if FFmpeg path is valid
        /// Проверить корректность пути к FFmpeg
        /// </summary>
        private bool TestFFmpeg(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;
                
                process.WaitForExit(2000);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generate thumbnail using FFmpeg
        /// Сгенерировать миниатюру используя FFmpeg
        /// </summary>
        private BitmapImage? GenerateThumbnailWithFFmpeg(long timeMs, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentVideoPath) || string.IsNullOrEmpty(_ffmpegPath) || _isDisposed)
                return null;

            string thumbnailPath = Path.Combine(_tempFolder, $"thumb_{Guid.NewGuid():N}.jpg");
            
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return null;

                // Format time for FFmpeg (HH:MM:SS.mmm)
                // Форматировать время для FFmpeg
                var time = TimeSpan.FromMilliseconds(timeMs);
                string timeStr = $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds:D3}";

                var psi = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = $"-ss {timeStr} -i \"{_currentVideoPath}\" -vframes 1 -s {ThumbnailWidth}x{ThumbnailHeight} -q:v 5 -y \"{thumbnailPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return null;

                // Wait with timeout / Ждать с таймаутом
                bool exited = process.WaitForExit(GenerationTimeoutMs);
                
                if (!exited)
                {
                    try { process.Kill(); } catch { }
                    return null;
                }

                if (cancellationToken.IsCancellationRequested)
                    return null;

                if (File.Exists(thumbnailPath) && new FileInfo(thumbnailPath).Length > 0)
                {
                    // Load image on UI thread compatible way / Загрузить изображение совместимо с UI потоком
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(thumbnailPath);
                    bitmap.EndInit();
                    bitmap.Freeze(); // Make thread-safe / Сделать потокобезопасным
                    
                    // Clean up temp file / Удалить временный файл
                    try { File.Delete(thumbnailPath); } catch { }
                    
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FFmpeg thumbnail error: {ex.Message}");
            }
            finally
            {
                // Ensure cleanup / Гарантировать очистку
                try { if (File.Exists(thumbnailPath)) File.Delete(thumbnailPath); } catch { }
            }
            
            return null;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                ClearCache();
                try { _generationSemaphore?.Dispose(); } catch { }
                
                // Clean up temp folder / Очистить временную папку
                try
                {
                    if (Directory.Exists(_tempFolder))
                    {
                        var files = Directory.GetFiles(_tempFolder, "thumb_*.jpg");
                        foreach (var file in files)
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                }
                catch { }
            }
        }

        #endregion
    }
}
