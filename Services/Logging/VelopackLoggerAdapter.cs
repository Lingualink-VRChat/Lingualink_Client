using System;
using System.Diagnostics;
using lingualink_client.Models;
using lingualink_client.Services.Interfaces;
using Velopack.Logging;

namespace lingualink_client.Services.Logging
{
    /// <summary>
    /// ? Velopack ????????????????
    /// </summary>
    public sealed class VelopackLoggerAdapter : IVelopackLogger
    {
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose()
            {
            }
        }

        private readonly object _syncRoot = new object();
        private ILoggingManager? _loggingManager;

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public void AttachLoggingManager(ILoggingManager loggingManager)
        {
            if (loggingManager is null)
            {
                throw new ArgumentNullException(nameof(loggingManager));
            }

            lock (_syncRoot)
            {
                _loggingManager = loggingManager;
            }
        }

        public void Log(VelopackLogLevel logLevel, string message, Exception? exception)
        {
            if (logLevel < VelopackLogLevel.Information)
            {
                Debug.WriteLine($"[Velopack:{logLevel}] {message}");
                return;
            }

            var formatted = FormatMessage(message, exception);
            var mappedLevel = MapLevel(logLevel);

            ILoggingManager? manager;
            lock (_syncRoot)
            {
                manager = _loggingManager;
            }

            if (manager is not null)
            {
                manager.AddMessage(formatted, mappedLevel, "Velopack");
            }
            else
            {
                Debug.WriteLine($"[Velopack:{logLevel}] {formatted}");
            }
        }

        private static string FormatMessage(string message, Exception? exception)
        {
            var text = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();

            if (exception is not null)
            {
                var exceptionText = exception.ToString();
                text = string.IsNullOrEmpty(text) ? exceptionText : $"{text}{Environment.NewLine}{exceptionText}";
            }

            return text;
        }

        private static LogLevel MapLevel(VelopackLogLevel logLevel)
        {
            return logLevel switch
            {
                VelopackLogLevel.Trace => LogLevel.Trace,
                VelopackLogLevel.Debug => LogLevel.Debug,
                VelopackLogLevel.Information => LogLevel.Info,
                VelopackLogLevel.Warning => LogLevel.Warning,
                VelopackLogLevel.Error => LogLevel.Error,
                VelopackLogLevel.Critical => LogLevel.Critical,
                _ => LogLevel.Info
            };
        }
    }
}