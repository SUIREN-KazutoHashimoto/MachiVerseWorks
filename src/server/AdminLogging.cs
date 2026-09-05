namespace MachiVerseWorks.Server;

internal static class AdminLogging
{
    private static readonly Action<ILogger, string, Exception?> CommandFailed = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(2001, nameof(CommandFailed)),
        "Admin command failed: {Command}");

    private static readonly Action<ILogger, string, Exception?> ConsoleInformation = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(2002, nameof(ConsoleInformation)),
        "{Message}");

    public static void LogError(this ILogger<AdminCommandExecutor> logger, Exception exception, string messageTemplate, string command)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(exception);
        _ = messageTemplate;
        CommandFailed(logger, command, exception);
    }

    public static void LogInformation(this ILogger<ServerConsoleService> logger, string message)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ConsoleInformation(logger, message, null);
    }
}
