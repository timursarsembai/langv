using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using LangVPlayer.Models;

namespace LangVPlayer.Services
{
    /// <summary>
    /// Service for parsing .srt subtitle files.
    /// Сервис для парсинга .srt файлов субтитров.
    /// </summary>
    public static class SrtParserService
    {
        // Regex pattern for SRT timestamp: 00:00:00,000 --> 00:00:00,000
        // Паттерн для таймстампа SRT: 00:00:00,000 --> 00:00:00,000
        private static readonly Regex TimestampRegex = new Regex(
            @"(\d{1,2}):(\d{2}):(\d{2})[,.](\d{3})\s*-->\s*(\d{1,2}):(\d{2}):(\d{2})[,.](\d{3})",
            RegexOptions.Compiled);

        /// <summary>
        /// Parse an .srt file and return a list of subtitle items.
        /// Парсинг .srt файла и возврат списка субтитров.
        /// </summary>
        /// <param name="filePath">Path to the .srt file / Путь к .srt файлу</param>
        /// <returns>List of parsed subtitles / Список распарсенных субтитров</returns>
        public static List<SubtitleItem> Parse(string filePath)
        {
            var subtitles = new List<SubtitleItem>();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return subtitles;
            }

            try
            {
                // Try to detect encoding, default to UTF-8
                // Попытка определить кодировку, по умолчанию UTF-8
                var encoding = DetectEncoding(filePath);
                var content = File.ReadAllText(filePath, encoding);
                
                // Normalize line endings / Нормализация переносов строк
                content = content.Replace("\r\n", "\n").Replace("\r", "\n");
                
                // Split into blocks (separated by empty lines) / Разделить на блоки (разделены пустыми строками)
                var blocks = content.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var block in blocks)
                {
                    var subtitle = ParseBlock(block.Trim());
                    if (subtitle != null)
                    {
                        subtitles.Add(subtitle);
                    }
                }

                // Sort by start time / Сортировка по времени начала
                subtitles.Sort((a, b) => a.StartTimeMs.CompareTo(b.StartTimeMs));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SRT parsing error: {ex.Message}");
            }

            return subtitles;
        }

        /// <summary>
        /// Parse a single subtitle block.
        /// Парсинг одного блока субтитров.
        /// </summary>
        private static SubtitleItem? ParseBlock(string block)
        {
            if (string.IsNullOrWhiteSpace(block))
                return null;

            var lines = block.Split('\n');
            if (lines.Length < 2)
                return null;

            // Find the timestamp line / Найти строку с таймстампом
            int timestampLineIndex = -1;
            for (int i = 0; i < Math.Min(lines.Length, 3); i++)
            {
                if (TimestampRegex.IsMatch(lines[i]))
                {
                    timestampLineIndex = i;
                    break;
                }
            }

            if (timestampLineIndex == -1)
                return null;

            // Parse timestamp / Парсинг таймстампа
            var match = TimestampRegex.Match(lines[timestampLineIndex]);
            if (!match.Success)
                return null;

            // Parse index (line before timestamp, if exists) / Парсинг индекса (строка перед таймстампом)
            int index = 0;
            if (timestampLineIndex > 0 && int.TryParse(lines[timestampLineIndex - 1].Trim(), out var parsedIndex))
            {
                index = parsedIndex;
            }

            // Parse start time / Парсинг времени начала
            long startTimeMs = ParseTimeToMs(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value),
                int.Parse(match.Groups[4].Value));

            // Parse end time / Парсинг времени окончания
            long endTimeMs = ParseTimeToMs(
                int.Parse(match.Groups[5].Value),
                int.Parse(match.Groups[6].Value),
                int.Parse(match.Groups[7].Value),
                int.Parse(match.Groups[8].Value));

            // Collect text lines (everything after timestamp) / Собрать текстовые строки (всё после таймстампа)
            var textBuilder = new StringBuilder();
            for (int i = timestampLineIndex + 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    if (textBuilder.Length > 0)
                        textBuilder.Append('\n');
                    textBuilder.Append(StripHtmlTags(line));
                }
            }

            if (textBuilder.Length == 0)
                return null;

            return new SubtitleItem
            {
                Index = index,
                StartTimeMs = startTimeMs,
                EndTimeMs = endTimeMs,
                Text = textBuilder.ToString()
            };
        }

        /// <summary>
        /// Convert hours, minutes, seconds, milliseconds to total milliseconds.
        /// Конвертировать часы, минуты, секунды, миллисекунды в общее количество миллисекунд.
        /// </summary>
        private static long ParseTimeToMs(int hours, int minutes, int seconds, int milliseconds)
        {
            return (hours * 3600000L) + (minutes * 60000L) + (seconds * 1000L) + milliseconds;
        }

        /// <summary>
        /// Remove HTML-like tags from subtitle text (e.g., &lt;i&gt;, &lt;b&gt;, &lt;font&gt;).
        /// Удалить HTML-подобные теги из текста субтитров.
        /// </summary>
        private static string StripHtmlTags(string text)
        {
            // Remove common subtitle formatting tags / Удалить типичные теги форматирования субтитров
            return Regex.Replace(text, @"<[^>]+>", string.Empty);
        }

        /// <summary>
        /// Detect file encoding (supports UTF-8 BOM, UTF-16, fallback to system default).
        /// Определить кодировку файла (поддерживает UTF-8 BOM, UTF-16, иначе системная по умолчанию).
        /// </summary>
        private static Encoding DetectEncoding(string filePath)
        {
            // Read first bytes to detect BOM / Читаем первые байты для определения BOM
            var bom = new byte[4];
            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            // UTF-8 with BOM
            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;

            // UTF-16 LE
            if (bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;

            // UTF-16 BE
            if (bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;

            // UTF-32 LE
            if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
                return Encoding.UTF32;

            // Default to UTF-8 without BOM (most common for modern .srt files)
            // По умолчанию UTF-8 без BOM (наиболее распространённый для современных .srt файлов)
            return Encoding.UTF8;
        }

        /// <summary>
        /// Find the subtitle that should be displayed at the given time.
        /// Binary search for efficiency with large subtitle files.
        /// Найти субтитр, который должен отображаться в указанное время.
        /// Бинарный поиск для эффективности с большими файлами субтитров.
        /// </summary>
        /// <param name="subtitles">Sorted list of subtitles / Отсортированный список субтитров</param>
        /// <param name="currentTimeMs">Current time in milliseconds / Текущее время в миллисекундах</param>
        /// <returns>Active subtitle or null / Активный субтитр или null</returns>
        public static SubtitleItem? FindActiveSubtitle(List<SubtitleItem> subtitles, long currentTimeMs)
        {
            if (subtitles == null || subtitles.Count == 0)
                return null;

            // Binary search for approximate position / Бинарный поиск для приблизительной позиции
            int left = 0;
            int right = subtitles.Count - 1;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                var subtitle = subtitles[mid];

                if (subtitle.IsActiveAt(currentTimeMs))
                {
                    return subtitle;
                }
                else if (currentTimeMs < subtitle.StartTimeMs)
                {
                    right = mid - 1;
                }
                else // currentTimeMs > subtitle.EndTimeMs
                {
                    left = mid + 1;
                }
            }

            return null;
        }
    }
}
