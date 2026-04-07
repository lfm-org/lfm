using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Lfm.Contracts.Privacy;

namespace Lfm.Api.Functions;

public class PrivacyContactFunction(ILogger<PrivacyContactFunction> log)
{
    [Function("privacy-contact")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "privacy/contact")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<ContactRequest>(
            req.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken: cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "invalid body" });

        var validator = new ContactRequestValidator();
        var validationResult = await validator.ValidateAsync(body, cancellationToken);
        if (!validationResult.IsValid)
            return new BadRequestObjectResult(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        // Log the contact request to App Insights (no PII).
        log.LogInformation("Privacy contact request received: type={Type}", body.Type);

        return new OkObjectResult(new { message = "Contact request received" });
    }
}
