using System;

namespace lingualink_client.Models
{
    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public class LogEntry
    {
        public LogEntry(LogLevel level, string message, string category, string? details = null)
        {
            Id = Guid.NewGuid();
            Timestamp = DateTime.Now;
            Level = level;
            Category = category;
            Message = message;
            Details = details;
        }

        public Guid Id { get; }
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public string? Details { get; set; }

        public string ToDisplayString()
        {
            var baseMessage = $"[{Timestamp:HH:mm:ss.fff}] [{Level}] [{Category}] {Message}";
            return string.IsNullOrWhiteSpace(Details) ? baseMessage : $"{baseMessage} :: {Details}";
        }
    }
}
