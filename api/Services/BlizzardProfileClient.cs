using System.Net.Http.Headers;
using System.Text.Json;
using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Services;

/// <summary>
/// Implements <see cref="IBlizzardProfileClient"/> using the Blizzard Profile/Game Data APIs.
///
/// The BaseAddress is set at registration time in Program.cs:
///   https://{region}.api.blizzard.com/
///
/// Profile namespace is derived from the configured region (e.g. "profile-eu").
/// </summary>
public sealed class BlizzardProfileClient : IBlizzardProfileClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly string _profileNamespace;

    public BlizzardProfileClient(HttpClient httpClient, IOptions<BlizzardOptions> options)
    {
        _httpClient = httpClient;
        _profileNamespace = $"profile-{options.Value.Region.ToLowerInvariant()}";
    }

    /// <inheritdoc/>
    public async Task<BlizzardAccountProfileSummary> GetAccountProfileSummaryAsync(
        string accessToken,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"profile/user/wow?namespace={Uri.EscapeDataString(_profileNamespace)}&locale=en_US");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<BlizzardAccountProfileSummary>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Blizzard profile endpoint returned empty response.");
    }
}
