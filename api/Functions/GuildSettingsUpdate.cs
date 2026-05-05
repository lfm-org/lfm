// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Lfm.Api.Helpers;
using Lfm.Api.Repositories;
using Lfm.Api.Validation;
using Lfm.Contracts.Guild;

namespace Lfm.Api.Functions;

internal static class GuildSettingsUpdate
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    internal static async Task<(UpdateGuildRequest? Request, IActionResult? Error)> ReadValidatedRequestAsync(
        HttpRequest req,
        CancellationToken cancellationToken)
    {
        UpdateGuildRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<UpdateGuildRequest>(
                req.Body,
                JsonOptions,
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return (null, Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing."));
        }

        if (body is null)
            return (null, Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing."));

        var validator = new UpdateGuildRequestValidator();
        var validationResult = await validator.ValidateAsync(body, cancellationToken);
        if (validationResult.IsValid)
            return (body, null);

        var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray();
        return (null, Problem.BadRequest(
            req.HttpContext,
            "validation-failed",
            "Request body failed validation.",
            new Dictionary<string, object?> { ["errors"] = errors }));
    }

    internal static GuildDocument Apply(GuildDocument guildDoc, UpdateGuildRequest body)
    {
        var initializedAt = guildDoc.Setup?.InitializedAt ?? DateTimeOffset.UtcNow.ToString("o");
        var updatedSetup = new GuildSetup(
            InitializedAt: initializedAt,
            Timezone: body.Timezone!,
            Locale: body.Locale!);

        var updatedRankPermissions = body.RankPermissions is not null
            ? body.RankPermissions
                .Select(rp => new GuildRankPermission(rp.Rank, rp.CanCreateGuildRuns, rp.CanSignupGuildRuns, rp.CanDeleteGuildRuns))
                .ToList()
            : guildDoc.RankPermissions;

        return guildDoc with
        {
            Slogan = body.Slogan ?? guildDoc.Slogan,
            Setup = updatedSetup,
            RankPermissions = updatedRankPermissions,
        };
    }

    internal static string? ResolveIfMatch(HttpRequest req)
    {
        if (!req.Headers.TryGetValue("If-Match", out var values))
            return null;
        var value = values.ToString();
        if (string.IsNullOrWhiteSpace(value) || value == "*")
            return null;
        return value;
    }

    internal static void EmitEtag(HttpRequest req, GuildDocument doc)
    {
        if (!string.IsNullOrEmpty(doc.ETag))
            req.HttpContext.Response.Headers.ETag = doc.ETag;
    }
}
