namespace Lfm.App.Services;

public sealed class CredentialsHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Browser automatically sends cookies on same-origin
        return base.SendAsync(request, ct);
    }
}
