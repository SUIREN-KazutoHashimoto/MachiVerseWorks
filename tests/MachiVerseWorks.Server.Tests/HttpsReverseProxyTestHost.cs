using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MachiVerseWorks.Server.Tests;

internal sealed class HttpsReverseProxyTestHost : IAsyncDisposable
{
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade",
    };

    private readonly WebApplication _app;
    private readonly HttpClient _backendClient;
    private readonly X509Certificate2 _certificate;
    private readonly RSA _certificateKey;
    private bool _stopped;

    private HttpsReverseProxyTestHost(
        WebApplication app,
        Uri httpsAddress,
        HttpClient backendClient,
        X509Certificate2 certificate,
        RSA certificateKey)
    {
        _app = app;
        HttpsAddress = httpsAddress;
        _backendClient = backendClient;
        _certificate = certificate;
        _certificateKey = certificateKey;
    }

    public Uri HttpsAddress { get; }

    public static async Task<HttpsReverseProxyTestHost> StartAsync(Uri backendAddress)
    {
        ArgumentNullException.ThrowIfNull(backendAddress);

        var certificateKey = RSA.Create(2048);
        var certificate = CreateCertificate(certificateKey);
        var backendClient = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
        })
        {
            BaseAddress = backendAddress,
        };

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0, listenOptions => listenOptions.UseHttps(certificate)));

        var app = builder.Build();
        app.Run(context => ForwardAsync(context, backendAddress, backendClient));

        try
        {
            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("HTTPS reverse proxy did not expose server addresses.");
            var address = addresses.Addresses.Single(value => value.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
            return new HttpsReverseProxyTestHost(app, new Uri(address), backendClient, certificate, certificateKey);
        }
        catch
        {
            await app.DisposeAsync();
            backendClient.Dispose();
            certificate.Dispose();
            certificateKey.Dispose();
            throw;
        }
    }

    public HttpClient CreateTrustedHttpClient()
    {
        var expectedThumbprint = _certificate.Thumbprint;
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            ServerCertificateCustomValidationCallback = (_, presentedCertificate, _, _) =>
                presentedCertificate is not null
                && string.Equals(presentedCertificate.Thumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase),
        };
        return new HttpClient(handler);
    }

    private static async Task ForwardAsync(HttpContext context, Uri backendAddress, HttpClient backendClient)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var target = new Uri(backendAddress, $"{context.Request.Path}{context.Request.QueryString}");
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);
        var hasBody = context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding");
        if (hasBody) request.Content = new StreamContent(context.Request.Body);

        foreach (var (name, values) in context.Request.Headers)
        {
            if (string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase) || HopByHopHeaders.Contains(name)) continue;
            if (request.Headers.TryAddWithoutValidation(name, values.ToArray())) continue;
            request.Content?.Headers.TryAddWithoutValidation(name, values.ToArray());
        }

        request.Headers.Remove("X-Forwarded-Proto");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.Remove("X-Forwarded-Host");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", context.Request.Host.Value);
        if (context.Connection.RemoteIpAddress is { } remoteAddress)
        {
            request.Headers.Remove("X-Forwarded-For");
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", remoteAddress.ToString());
        }

        using var response = await backendClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;

        CopyResponseHeaders(response.Headers, context.Response.Headers);
        CopyResponseHeaders(response.Content.Headers, context.Response.Headers);
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    private static void CopyResponseHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> source, IHeaderDictionary destination)
    {
        foreach (var (name, values) in source)
        {
            if (HopByHopHeaders.Contains(name)) continue;
            destination[name] = values.ToArray();
        }
    }

    private static X509Certificate2 CreateCertificate(RSA certificateKey)
    {
        var request = new CertificateRequest("CN=localhost", certificateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stopped)
        {
            _stopped = true;
            await _app.StopAsync();
        }

        await _app.DisposeAsync();
        _backendClient.Dispose();
        _certificate.Dispose();
        _certificateKey.Dispose();
    }
}
