// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.ComponentModel.DataAnnotations;

namespace Lfm.Api.Options;

/// <summary>
/// Configuration for the <see cref="Services.IIdempotencyStore"/> and the
/// <see cref="Middleware.IdempotencyMiddleware"/> that sits in front of it.
/// </summary>
public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    /// <summary>
    /// Name of the Cosmos container. Must match the Bicep-provisioned container
    /// in <c>infra/modules/cosmos.bicep</c>. Partition key is <c>/battleNetId</c>,
    /// the document id is <c>{battleNetId}:{idempotencyKey}</c>.
    /// </summary>
    [Required]
    public string ContainerName { get; init; } = "idempotency";

    /// <summary>
    /// Cosmos per-document TTL in seconds for idempotency entries. 24 hours is
    /// long enough to cover any retry burst from flaky networks or background
    /// schedulers without growing the container beyond the free-tier budget.
    /// </summary>
    [Range(60, 604800)]
    public int TtlSeconds { get; init; } = 86400;
}
