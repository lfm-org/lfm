// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Characters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Lfm.Api.Functions;

/// <summary>
/// POST /api/battlenet/character-portraits
///
/// Accepts a JSON array of <c>{ region, realm, name }</c> objects and returns a
/// map of "{region}-{realm}-{name}" → portrait URL for each character that has a
/// resolvable portrait.
///
/// Portrait URL resolution order (see <see cref="ICharacterPortraitService"/>):
///   1. Stored character's <c>portraitUrl</c> (Blizzard CDN URL).
///   2. Stored character's <c>mediaSummary.assets[key="avatar"]</c>.
///   3. The raider's <c>portraitCache</c> map.
///   4. Blizzard character-media API call using the session access token.
///
/// Characters with no resolvable portrait are omitted from the result.
///
/// This mirrors the TypeScript handler in
/// <c>functions/src/functions/battlenet-character-portraits.ts</c>.
/// </summary>
public class BattleNetCharacterPortraitsFunction(
    IRaidersRepository repo,
    ICharacterPortraitService portraitService)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Function("battlenet-character-portraits")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "battlenet/character-portraits")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal();

        // Deserialise the request body.
        List<CharacterPortraitRequest>? requests;
        try
        {
            requests = await JsonSerializer.DeserializeAsync<List<CharacterPortraitRequest>>(
                req.Body, _jsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            requests = null;
        }

        if (requests is null || requests.Count == 0)
            return new OkObjectResult(new PortraitResponse(new Dictionary<string, string>()));

        var raider = await repo.GetByBattleNetIdAsync(principal.BattleNetId, cancellationToken);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        // Access token is stored in the session principal (populated at OAuth callback time).
        // If absent (old session before B2.5), fall back to empty string; the Blizzard fetch
        // will return a non-2xx and the portrait will simply be omitted from the result.
        var accessToken = principal.AccessToken ?? string.Empty;

        var response = await portraitService.ResolveAsync(raider, requests, accessToken, cancellationToken);
        return new OkObjectResult(response);
    }

    /// <summary>
    /// <c>/api/v1/battlenet/character-portraits</c> alias for <see cref="Run"/>.
    /// See <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("battlenet-character-portraits-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/battlenet/character-portraits")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
        => Run(req, ctx, cancellationToken);
}
