using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LangVPlayer.Models;

namespace LangVPlayer.Services
{
    /// <summary>
    /// Service for extracting embedded subtitles from video files using FFmpeg.
    /// Сервис для извлечения встроенных субтитров из видеофайлов с помощью FFmpeg.
    /// </summary>
    public class SubtitleExtractorService
    {
        private readonly string _ffmpegPath;
        
        /// <summary>
        /// Information about a subtitle track in a video file.
        /// Информация о дорожке субтитров в видеофайле.
        /// </summary>
        public class SubtitleTrackInfo
        {
            public int StreamIndex { get; set; }
            public string Language { get; set; } = "";
            public string Title { get; set; } = "";
            public string Codec { get; set; } = "";
            
            /// <summary>
            /// Get display name with language code mapping.
            /// Получить отображаемое имя с маппингом кодов языков.
            /// </summary>
            public string DisplayName 
            {
                get
                {
                    var langName = MapLanguageCode(Language);
                    
                    // If we have a title, use it with language / Если есть название, используем его с языком
                    if (!string.IsNullOrEmpty(Title))
                    {
                        // If title already contains language info, just return it
                        // Если название уже содержит инфо о языке, просто вернуть его
                        if (Title.Contains('[') || Title.Contains('(') || 
                            Title.ToLower().Contains("рус") || Title.ToLower().Contains("eng"))
                            return Title;
                        
                        // Add language in brackets if we have it / Добавить язык в скобках если он есть
                        if (!string.IsNullOrEmpty(langName))
                            return $"{Title} [{langName}]";
                        
                        return Title;
                    }
                    
                    // Map language codes to readable names / Маппинг кодов языков на читаемые названия
                    if (!string.IsNullOrEmpty(langName))
                        return $"{langName} (Track {StreamIndex + 1})";
                    
                    // Fallback to track number / Запасной вариант - номер дорожки
                    return $"Track {StreamIndex + 1}";
                }
            }
            
            private static string MapLanguageCode(string code)
            {
                if (string.IsNullOrEmpty(code)) return "";
                
                return code.ToLower() switch
                {
                    "eng" or "en" => "English",
                    "rus" or "ru" => "Русский",
                    "ukr" or "uk" => "Українська",
                    "spa" or "es" => "Español",
                    "fra" or "fr" => "Français",
                    "deu" or "de" => "Deutsch",
                    "ita" or "it" => "Italiano",
                    "por" or "pt" => "Português",
                    "jpn" or "ja" => "日本語",
                    "kor" or "ko" => "한국어",
                    "chi" or "zh" => "中文",
                    "ara" or "ar" => "العربية",
                    "hin" or "hi" => "हिन्दी",
                    "tur" or "tr" => "Türkçe",
                    "pol" or "pl" => "Polski",
                    "nld" or "nl" => "Nederlands",
                    "swe" or "sv" => "Svenska",
                    "nor" or "no" => "Norsk",
                    "fin" or "fi" => "Suomi",
                    "dan" or "da" => "Dansk",
                    "ces" or "cs" => "Čeština",
                    "hun" or "hu" => "Magyar",
                    "ron" or "ro" => "Română",
                    "bul" or "bg" => "Български",
                    "ell" or "el" => "Ελληνικά",
                    "heb" or "he" => "עברית",
                    "tha" or "th" => "ไทย",
                    "vie" or "vi" => "Tiếng Việt",
                    "ind" or "id" => "Bahasa Indonesia",
                    "msa" or "ms" => "Bahasa Melayu",
                    "und" => "Unknown",
                    _ => code.ToUpper() // Return code as-is if not mapped
                };
            }
        }

        public SubtitleExtractorService(string ffmpegPath)
        {
            _ffmpegPath = ffmpegPath;
        }

        /// <summary>
        /// Check if FFmpeg is available.
        /// Проверить доступность FFmpeg.
        /// </summary>
        public bool IsAvailable => File.Exists(_ffmpegPath);

        /// <summary>
        /// Get list of subtitle tracks in a video file.
        /// Получить список дорожек субтитров в видеофайле.
        /// </summary>
        public async Task<List<SubtitleTrackInfo>> GetSubtitleTracksAsync(string videoPath, CancellationToken ct = default)
        {
            var tracks = new List<SubtitleTrackInfo>();
            
            if (!IsAvailable || string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
                return tracks;

            try
            {
                // Use ffprobe to get stream info / Используем ffprobe для получения информации о потоках
                var ffprobePath = Path.Combine(Path.GetDirectoryName(_ffmpegPath) ?? "", "ffprobe.exe");
                if (!File.Exists(ffprobePath))
                {
                    // Try using ffmpeg -i instead / Попробуем использовать ffmpeg -i
                    return await GetSubtitleTracksViaFfmpegAsync(videoPath, ct);
                }

                var psi = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = $"-v quiet -print_format json -show_streams -select_streams s \"{videoPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process == null) return tracks;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(ct);

                // Parse JSON output / Парсинг JSON вывода
                tracks = ParseFfprobeOutput(output);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting subtitle tracks: {ex.Message}");
            }

            return tracks;
        }

        /// <summary>
        /// Get subtitle tracks using ffmpeg -i (fallback method).
        /// Получить дорожки субтитров через ffmpeg -i (запасной метод).
        /// </summary>
        private async Task<List<SubtitleTrackInfo>> GetSubtitleTracksViaFfmpegAsync(string videoPath, CancellationToken ct)
        {
            var tracks = new List<SubtitleTrackInfo>();

            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-i \"{videoPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process == null) return tracks;

            // FFmpeg outputs to stderr / FFmpeg выводит в stderr
            var output = await process.StandardError.ReadToEndAsync();
            
            try { await process.WaitForExitAsync(ct); } catch { }

            // Parse output for subtitle streams / Парсим вывод для поиска субтитров
            // Format: Stream #0:2(eng): Subtitle: subrip
            //         Metadata:
            //           title           : English
            var lines = output.Split('\n');
            int subtitleIndex = 0;
            SubtitleTrackInfo? currentTrack = null;
            bool inMetadata = false;
            
            foreach (var line in lines)
            {
                // Check for Stream line with Subtitle / Проверка на строку Stream с Subtitle
                if (line.Contains("Stream") && line.Contains("Subtitle:"))
                {
                    // Save previous track if exists / Сохранить предыдущий трек если есть
                    if (currentTrack != null)
                    {
                        tracks.Add(currentTrack);
                    }
                    
                    currentTrack = new SubtitleTrackInfo { StreamIndex = subtitleIndex++ };
                    inMetadata = false;
                    
                    // Extract language from (eng) / Извлечь язык из (eng)
                    var langMatch = System.Text.RegularExpressions.Regex.Match(line, @"Stream #\d+:\d+\((\w+)\)");
                    if (langMatch.Success)
                    {
                        currentTrack.Language = langMatch.Groups[1].Value;
                    }
                    
                    // Extract codec / Извлечь кодек
                    var codecMatch = System.Text.RegularExpressions.Regex.Match(line, @"Subtitle:\s*(\w+)");
                    if (codecMatch.Success)
                    {
                        currentTrack.Codec = codecMatch.Groups[1].Value;
                    }
                    
                    Debug.WriteLine($"Found subtitle stream: lang={currentTrack.Language}, codec={currentTrack.Codec}");
                }
                // Check for Metadata section / Проверка на секцию Metadata
                else if (line.Trim().StartsWith("Metadata:"))
                {
                    inMetadata = true;
                }
                // Extract title from metadata / Извлечь title из метаданных
                // FFmpeg format:     title           : Full (русский)
                else if (inMetadata && currentTrack != null)
                {
                    var trimmedLine = line.Trim();
                    // Check if line starts with "title" (case insensitive)
                    if (trimmedLine.StartsWith("title", StringComparison.OrdinalIgnoreCase))
                    {
                        var colonIndex = trimmedLine.IndexOf(':');
                        if (colonIndex > 0 && colonIndex < trimmedLine.Length - 1)
                        {
                            currentTrack.Title = trimmedLine.Substring(colonIndex + 1).Trim();
                            Debug.WriteLine($"Found title: '{currentTrack.Title}'");
                        }
                    }
                }
                // End of metadata section (next stream or other section) / Конец секции метаданных
                else if (inMetadata && line.Contains("Stream"))
                {
                    inMetadata = false;
                }
            }
            
            // Don't forget the last track / Не забыть последний трек
            if (currentTrack != null)
            {
                tracks.Add(currentTrack);
            }

            return tracks;
        }

        /// <summary>
        /// Parse ffprobe JSON output.
        /// Парсинг JSON вывода ffprobe.
        /// </summary>
        private List<SubtitleTrackInfo> ParseFfprobeOutput(string json)
        {
            var tracks = new List<SubtitleTrackInfo>();
            
            try
            {
                // Simple JSON parsing without external dependencies
                // Простой парсинг JSON без внешних зависимостей
                if (string.IsNullOrEmpty(json) || !json.Contains("streams"))
                    return tracks;

                // Find streams array / Найти массив streams
                int streamsStart = json.IndexOf("\"streams\"");
                if (streamsStart < 0) return tracks;

                int arrayStart = json.IndexOf('[', streamsStart);
                int arrayEnd = json.LastIndexOf(']');
                if (arrayStart < 0 || arrayEnd < 0) return tracks;

                var streamsJson = json.Substring(arrayStart, arrayEnd - arrayStart + 1);
                
                // Split by stream objects / Разделить на объекты потоков
                int streamIndex = 0;
                int braceCount = 0;
                int streamStart = -1;
                
                for (int i = 0; i < streamsJson.Length; i++)
                {
                    if (streamsJson[i] == '{')
                    {
                        if (braceCount == 0) streamStart = i;
                        braceCount++;
                    }
                    else if (streamsJson[i] == '}')
                    {
                        braceCount--;
                        if (braceCount == 0 && streamStart >= 0)
                        {
                            var streamJson = streamsJson.Substring(streamStart, i - streamStart + 1);
                            var track = ParseStreamJson(streamJson, streamIndex++);
                            if (track != null)
                            {
                                tracks.Add(track);
                            }
                            streamStart = -1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing ffprobe output: {ex.Message}");
            }

            return tracks;
        }

        /// <summary>
        /// Parse a single stream JSON object.
        /// Парсинг одного JSON объекта потока.
        /// </summary>
        private SubtitleTrackInfo? ParseStreamJson(string json, int index)
        {
            // Extract values using simple string search / Извлечь значения простым поиском
            string GetValue(string key)
            {
                var pattern = $"\"{key}\"\\s*:\\s*\"([^\"]+)\"";
                var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
                return match.Success ? match.Groups[1].Value : "";
            }
            
            int GetIntValue(string key)
            {
                var pattern = $"\"{key}\"\\s*:\\s*(\\d+)";
                var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
                return match.Success && int.TryParse(match.Groups[1].Value, out int val) ? val : -1;
            }

            var codecType = GetValue("codec_type");
            if (codecType != "subtitle") return null;

            return new SubtitleTrackInfo
            {
                StreamIndex = GetIntValue("index"),
                Language = GetValue("language"),
                Codec = GetValue("codec_name"),
                Title = GetValue("title")
            };
        }

        /// <summary>
        /// Extract subtitles from a video file to SRT format.
        /// Извлечь субтитры из видеофайла в формат SRT.
        /// </summary>
        public async Task<List<SubtitleItem>> ExtractSubtitlesAsync(
            string videoPath, 
            int streamIndex, 
            CancellationToken ct = default)
        {
            if (!IsAvailable || string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
                return new List<SubtitleItem>();

            try
            {
                // Create temp file for extracted subtitles / Создать временный файл для извлечённых субтитров
                var tempSrtPath = Path.Combine(Path.GetTempPath(), $"langv_sub_{Guid.NewGuid()}.srt");

                var psi = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    // -map 0:s:N selects Nth subtitle stream / -map 0:s:N выбирает N-ую дорожку субтитров
                    Arguments = $"-y -i \"{videoPath}\" -map 0:s:{streamIndex} -c:s srt \"{tempSrtPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return new List<SubtitleItem>();

                // Read stderr for error logging / Читаем stderr для логирования ошибок
                var stderrTask = process.StandardError.ReadToEndAsync();
                
                // Wait for extraction with timeout / Ждём извлечения с таймаутом
                var completed = await Task.Run(() => process.WaitForExit(30000), ct);
                
                var stderr = await stderrTask;
                Debug.WriteLine($"FFmpeg extraction stderr: {stderr}");
                
                if (!completed)
                {
                    try { process.Kill(); } catch { }
                    return new List<SubtitleItem>();
                }

                if (File.Exists(tempSrtPath))
                {
                    // Parse the extracted SRT file / Парсим извлечённый SRT файл
                    var subtitles = SrtParserService.Parse(tempSrtPath);
                    
                    // Clean up temp file / Удалить временный файл
                    try { File.Delete(tempSrtPath); } catch { }
                    
                    Debug.WriteLine($"Extracted {subtitles.Count} subtitles from stream {streamIndex}");
                    return subtitles;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting subtitles: {ex.Message}");
            }

            return new List<SubtitleItem>();
        }
    }
}
