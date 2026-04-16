// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Contracts.Me;

namespace Lfm.Api.Functions;

public class MeUpdateFunction(IRaidersRepository repo)
{
    private const int TtlDays = 180;
    private const int TtlSeconds = TtlDays * 86400;

    [Function("me-update")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "me")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        var body = await JsonSerializer.DeserializeAsync<UpdateMeRequest>(
            req.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken: cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "invalid body" });

        var validator = new UpdateMeRequestValidator();
        var validationResult = await validator.ValidateAsync(body, cancellationToken);
        if (!validationResult.IsValid)
            return new BadRequestObjectResult(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        // Read-modify-write: load existing doc then upsert with updated locale + TTL.
        var raider = await repo.GetByBattleNetIdAsync(principal.BattleNetId, cancellationToken);
        if (raider is null)
            return new NotFoundResult();

        var updated = raider with { Locale = body.Locale!, Ttl = TtlSeconds };
        await repo.UpsertAsync(updated, cancellationToken);

        return new OkObjectResult(new UpdateMeResponse(Locale: body.Locale!));
    }
}
