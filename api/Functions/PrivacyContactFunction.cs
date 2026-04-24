// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Helpers;
using Lfm.Api.Options;
using Lfm.Contracts.Privacy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves the privacy contact email used on the privacy page. The SPA fetches
/// this lazily behind a click-to-reveal button so the address is never in the
/// static HTML — see <c>app/Pages/PrivacyPolicyPage.razor</c>.
///
/// <para>
/// The response splits the address into <c>local</c> / <c>domain</c> fields
/// so a naive HTML scraper that just grabs the body does not receive a
/// pre-assembled address; the SPA reassembles at render time. The legacy
/// <c>email</c> field is still populated for one release to keep older
/// clients working — see <c>PrivacyEmailResponse</c>.
/// </para>
/// </summary>
public class PrivacyContactFunction(IOptions<PrivacyContactOptions> options)
{
    private readonly PrivacyContactOptions _options = options.Value;

    [Function("privacy-email")]
    public IActionResult GetEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "privacy-contact/email")] HttpRequest req)
    {
        var email = _options.Email;
        if (string.IsNullOrWhiteSpace(email))
            return Problem.NotFound(req.HttpContext, "privacy-email-unconfigured", "Privacy contact email is not configured for this deployment.");

        var at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1)
        {
            // A malformed address means the configuration is broken upstream
            // (validator on Options should already have rejected this). Hide
            // the detail from the caller and return 404 so we don't leak
            // partial data.
            return Problem.NotFound(req.HttpContext, "privacy-email-unconfigured", "Privacy contact email is not configured for this deployment.");
        }

        var local = email[..at];
        var domain = email[(at + 1)..];

#pragma warning disable CS0618 // Transitional: populate the obsolete Email field for one release.
        return new OkObjectResult(new PrivacyEmailResponse(Local: local, Domain: domain, Email: email));
#pragma warning restore CS0618
    }

    /// <summary>
    /// <c>/api/v1/privacy-contact/email</c> alias for <see cref="GetEmail"/>.
    /// See <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("privacy-email-v1")]
    public IActionResult GetEmailV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/privacy-contact/email")] HttpRequest req)
        => GetEmail(req);
}
