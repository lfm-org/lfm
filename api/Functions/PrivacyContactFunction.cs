// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Lfm.Api.Helpers;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves the contact email used on the privacy page. The SPA fetches this
/// lazily behind a click-to-reveal button so the address is never in the
/// static HTML — see <c>app/Pages/PrivacyPolicyPage.razor</c>.
/// </summary>
public class PrivacyContactFunction(IConfiguration config)
{
    [Function("privacy-email")]
    public IActionResult GetEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "privacy-contact/email")] HttpRequest req)
    {
        var email = config["PRIVACY_EMAIL"];
        if (string.IsNullOrEmpty(email))
            return Problem.NotFound(req.HttpContext, "privacy-email-unconfigured", "Privacy contact email is not configured for this deployment.");

        return new OkObjectResult(new { email });
    }
}
