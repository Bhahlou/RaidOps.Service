using Microsoft.Extensions.Logging;

namespace RaidOps.IntegrationTests.Infrastructure.Stubs;

/// <summary>
/// Minimal <see cref="ILogger"/> that records every entry, for asserting on what code invoked by
/// reflection (which can't take a typed logger mock) logged.
/// </summary>
internal sealed class ListLogger : ILogger
{
    /// <summary>Everything logged so far, as (level, formatted message).</summary>
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}
