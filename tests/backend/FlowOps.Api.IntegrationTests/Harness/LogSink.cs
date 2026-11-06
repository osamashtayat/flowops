using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>
/// Captures the app's log entries, with their structured values, so tests can assert on audit logging
/// (e.g. "admin X reset the password of user Y") and on what must never be logged.
/// </summary>
public sealed class LogSink : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => [.. _entries];

    public void Clear() => _entries.Clear();

    public ILogger CreateLogger(string categoryName) => new SinkLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class SinkLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            entries.Enqueue(new LogEntry(category, LogLevel.None, $"scope: {state}", new Dictionary<string, object?>(), null));
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), values.ToDictionary(v => v.Key, v => v.Value), exception?.ToString()));
        }
    }
}

public sealed record LogEntry(string Category, LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Values, string? Exception)
{
    /// <summary>True if <paramref name="text"/> appears anywhere in the entry: message, structured values, scope or exception.</summary>
    public bool Mentions(string text) =>
        Message.Contains(text, StringComparison.Ordinal)
        || (Exception?.Contains(text, StringComparison.Ordinal) ?? false)
        || Values.Values.Any(v => v?.ToString()?.Contains(text, StringComparison.Ordinal) ?? false);
}
