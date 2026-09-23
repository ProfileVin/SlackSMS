using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackSms.Application.Abstractions;

namespace SlackSms.Infrastructure.SignalWire;

/// <summary>Sends SMS through SignalWire's Compatibility (LaML) REST API.</summary>
public sealed class SignalWireClient(HttpClient http, IOptions<SignalWireOptions> options, ILogger<SignalWireClient> logger)
    : ISmsSender
{
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
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ProjectId}:{_options.ApiToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

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
}
