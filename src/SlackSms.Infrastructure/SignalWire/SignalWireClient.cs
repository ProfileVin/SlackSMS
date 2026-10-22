using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackSms.Application.Abstractions;

namespace SlackSms.Infrastructure.SignalWire;

/// <summary>Sends SMS and downloads inbound media through SignalWire's Compatibility (LaML) REST API.</summary>
public sealed class SignalWireClient(HttpClient http, IOptions<SignalWireOptions> options, ILogger<SignalWireClient> logger)
    : ISmsSender, IMediaReader
{
    /// <summary>Text parts longer than this are left as links.</summary>
    public const int MaxTextMediaBytes = 64 * 1024;

    private readonly SignalWireOptions _options = options.Value;

    public static string GetBaseAddress(string space)
    {
        var host = space.Trim().Replace("https://", "").TrimEnd('/');
        if (!host.Contains('.'))
            host += ".signalwire.com";
        return $"https://{host}/";
    }

    public async Task SendSmsAsync(string to, string body, CancellationToken ct)
    {
        var path = $"api/laml/2010-04-01/Accounts/{Uri.EscapeDataString(_options.ProjectId)}/Messages.json";
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["From"] = _options.FromNumber,
                ["To"] = to,
                ["Body"] = body,
            }),
        };
        request.Headers.Authorization = BasicAuth();

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new SmsSendException("Network error calling SignalWire", isTransient: true, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new SmsSendException("Timeout calling SignalWire", isTransient: true, ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("SMS sent to {To} ({Length} chars)", to, body.Length);
                return;
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            var transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            throw new SmsSendException($"SignalWire returned {(int)response.StatusCode}: {error}", transient);
        }
    }

    public async Task<string?> ReadTextAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            logger.LogWarning("Ignoring media with invalid URL {Url}", url);
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            // Only send our credentials to our own space, never to a host named in the webhook.
            var spaceHost = new Uri(GetBaseAddress(_options.Space)).Host;
            if (uri.Scheme == Uri.UriSchemeHttps && string.Equals(uri.Host, spaceHost, StringComparison.OrdinalIgnoreCase))
                request.Headers.Authorization = BasicAuth();

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Downloading media {Url} returned {StatusCode}", url, (int)response.StatusCode);
                return null;
            }

            await response.Content.LoadIntoBufferAsync(MaxTextMediaBytes, ct);
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Failed to download media {Url}", url);
            return null;
        }
    }

    private AuthenticationHeaderValue BasicAuth() =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ProjectId}:{_options.ApiToken}")));
}
