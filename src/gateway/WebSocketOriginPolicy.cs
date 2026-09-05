namespace MachiVerseWorks.Server;

internal sealed class WebSocketOriginPolicy
{
    private readonly HashSet<string> _allowedOrigins;

    public WebSocketOriginPolicy(IEnumerable<string> allowedOrigins)
    {
        ArgumentNullException.ThrowIfNull(allowedOrigins);
        _allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var origin in allowedOrigins)
        {
            _allowedOrigins.Add(NormalizeOrigin(origin));
        }
    }

    public bool IsAllowed(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        try
        {
            return _allowedOrigins.Contains(NormalizeOrigin(origin));
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static string NormalizeOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin) ||
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                $"WebSocket origin '{origin}' must be an absolute http/https origin without path, query, fragment, or credentials.");
        }

        return uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
    }
}
