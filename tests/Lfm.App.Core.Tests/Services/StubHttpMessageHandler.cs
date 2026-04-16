// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Net.Http.Json;

namespace Lfm.App.Core.Tests.Services;

/// <summary>
/// Deterministic HttpMessageHandler that returns a pre-configured response for
/// the first request and records the request for assertions. Reusable across
/// all app-client tests (RunsClient, MeClient, GuildClient, etc.).
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public int CallCount { get; private set; }

    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(HttpStatusCode statusCode, object? body = null)
        : this(_ => CreateResponse(statusCode, body))
    {
    }

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public static StubHttpMessageHandler Json(HttpStatusCode statusCode, object body) =>
        new(statusCode, body);

    public static StubHttpMessageHandler Throws(Exception exception) =>
        new(_ => throw exception);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        CallCount++;
        return Task.FromResult(_responder(request));
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, object? body) =>
        body is null
            ? new HttpResponseMessage(statusCode)
            : new HttpResponseMessage(statusCode) { Content = JsonContent.Create(body) };
}
