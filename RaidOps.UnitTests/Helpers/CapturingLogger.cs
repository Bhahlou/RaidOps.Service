using Microsoft.Extensions.Logging;

namespace RaidOps.UnitTests.Helpers;

/// <summary>
/// Minimal <see cref="ILogger{TCategoryName}"/> that records every log call, so tests can assert on
/// level, message and exception without going through Moq's generic-method matching. Thread-safe,
/// since handlers under test may log from parallel loops.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

    /// <summary>A snapshot of everything logged so far.</summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_entries)
                return _entries.ToList();
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_entries)
            _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    /// <summary>One captured log call.</summary>
    internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
