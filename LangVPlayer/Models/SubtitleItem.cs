using System;

namespace LangVPlayer.Models
{
    /// <summary>
    /// Represents a single subtitle entry with timing and text.
    /// Представляет одну запись субтитров с таймингом и текстом.
    /// </summary>
    public class SubtitleItem
    {
        /// <summary>
        /// Subtitle index number (1-based as in .srt files).
        /// Номер субтитра (начиная с 1, как в .srt файлах).
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Start time of the subtitle in milliseconds.
        /// Время начала субтитра в миллисекундах.
        /// </summary>
        public long StartTimeMs { get; set; }

        /// <summary>
        /// End time of the subtitle in milliseconds.
        /// Время окончания субтитра в миллисекундах.
        /// </summary>
        public long EndTimeMs { get; set; }

        /// <summary>
        /// The subtitle text (may contain multiple lines).
        /// Текст субтитра (может содержать несколько строк).
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Check if this subtitle should be displayed at the given time.
        /// Проверить, должен ли этот субтитр отображаться в указанное время.
        /// </summary>
        /// <param name="currentTimeMs">Current playback time in milliseconds / Текущее время воспроизведения в мс</param>
        /// <returns>True if subtitle is active / True если субтитр активен</returns>
        public bool IsActiveAt(long currentTimeMs)
        {
            return currentTimeMs >= StartTimeMs && currentTimeMs <= EndTimeMs;
        }

        public override string ToString()
        {
            var start = TimeSpan.FromMilliseconds(StartTimeMs);
            var end = TimeSpan.FromMilliseconds(EndTimeMs);
            return $"[{Index}] {start:hh\\:mm\\:ss\\,fff} --> {end:hh\\:mm\\:ss\\,fff}: {Text}";
        }
    }
}
