// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Lfm.App.Services;
using Lfm.Contracts.Runs;
using Moq;
using Xunit;

namespace Lfm.App.Core.Tests.Services;

public class RunsClientTests
{
    // Anchored to UtcNow so these fixtures never become time bombs against a
    // future-dated assertion. See issue #49.
    private static readonly string FutureStartTime =
        DateTimeOffset.UtcNow.AddDays(30).ToString("o");
    private static readonly string FutureSignupCloseTime =
        DateTimeOffset.UtcNow.AddDays(30).AddHours(-2).ToString("o");
    private static readonly string PastCreatedAt =
        DateTimeOffset.UtcNow.AddDays(-14).ToString("o");

    private static (RunsClient client, StubHttpMessageHandler handler) MakeClient(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7071/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("api")).Returns(http);

        return (new RunsClient(factory.Object), handler);
    }

    private static RunSummaryDto MakeSummary(string id = "run-1") =>
        new(
            Id: id,
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "Test run",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            CreatorGuild: "Stormchasers",
            CreatorGuildId: 42,
            InstanceId: 1,
            InstanceName: "Liberation of Undermine",
            CreatorBattleNetId: "player#1234",
            CreatedAt: PastCreatedAt,
            Ttl: 604800,
            RunCharacters: []);

    private static RunDetailDto MakeDetail(string id = "run-1") =>
        new(
            Id: id,
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "Test run",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            CreatorGuild: "Stormchasers",
            CreatorGuildId: 42,
            InstanceId: 1,
            InstanceName: "Liberation of Undermine",
            CreatorBattleNetId: "player#1234",
            CreatedAt: PastCreatedAt,
            Ttl: 604800,
            RunCharacters: []);

    private static CreateRunRequest MakeCreateRequest() =>
        new(
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "desc",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            InstanceId: 1,
            InstanceName: "Liberation of Undermine");

    [Fact]
    public async Task ListAsync_deserializes_items_from_runs_list_response_envelope()
    {
        // Server now returns a RunsListResponse envelope ({ items, continuationToken })
        // — the client must deserialize it and surface only the items through the
        // existing IRunsClient.ListAsync signature.
        var envelope = new RunsListResponse(
            Items: [MakeSummary("run-a"), MakeSummary("run-b")],
            ContinuationToken: "next-page");
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, envelope));

        var result = await client.ListAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("run-a", result[0].Id);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/api/runs", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task ListAsync_returns_empty_list_when_envelope_items_is_empty()
    {
        var envelope = new RunsListResponse(Items: [], ContinuationToken: null);
        var (client, _) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, envelope));

        var result = await client.ListAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_returns_empty_list_when_body_is_null()
    {
        // Server returns 200 with literal "null" body — RunsClient must coalesce
        // to an empty list at the public boundary, not propagate null.
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json"),
        });
        var (client, _) = MakeClient(handler);

        var result = await client.ListAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAsync_escapes_run_id_in_path()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));

        await client.GetAsync("run with spaces/and?weird", CancellationToken.None);

        var path = handler.LastRequest!.RequestUri!.PathAndQuery;
        Assert.StartsWith("/api/runs/", path);
        Assert.Contains("%2F", path);
        Assert.DoesNotContain(" ", path);
        Assert.DoesNotContain("?weird", path);
    }

    [Fact]
    public async Task GetAsync_throws_on_404_not_found()
    {
        // Pin current contract: GetFromJsonAsync<T> throws HttpRequestException on
        // non-2xx responses. If a future refactor wants to swallow 404s and return
        // null, this test must be updated deliberately.
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("missing-run", CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_posts_request_body_as_json()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));
        var request = MakeCreateRequest();

        var result = await client.CreateAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/api/runs", handler.LastRequest.RequestUri!.PathAndQuery);
        Assert.NotNull(handler.LastRequest.Content);
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task CreateAsync_returns_null_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.BadRequest));
        var request = MakeCreateRequest();

        var result = await client.CreateAsync(request, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_returns_true_on_success()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NoContent));

        var result = await client.DeleteAsync("run-1", CancellationToken.None);

        Assert.True(result);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task DeleteAsync_returns_false_on_failure()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.Forbidden));

        var result = await client.DeleteAsync("run-1", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task SignupAsync_posts_to_signup_subpath()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));
        var request = new SignupRequest(CharacterId: "char-1", DesiredAttendance: "IN", SpecId: null);

        var result = await client.SignupAsync("run-1", request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/api/runs/run-1/signup", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task UpdateAsync_puts_request_body_to_run_path()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));
        var request = new UpdateRunRequest(
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "updated",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            InstanceId: 1,
            InstanceName: "Liberation of Undermine");

        var result = await client.UpdateAsync("run-1", request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.Equal("/api/runs/run-1", handler.LastRequest.RequestUri!.PathAndQuery);
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task UpdateAsync_returns_null_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.BadRequest));
        var request = new UpdateRunRequest(null, null, null, null, null, null, null);

        var result = await client.UpdateAsync("run-1", request, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CancelSignupAsync_deletes_signup_subpath()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));

        var result = await client.CancelSignupAsync("run-1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("run-1", result!.Id);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal("/api/runs/run-1/signup", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task CancelSignupAsync_returns_null_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NotFound));

        var result = await client.CancelSignupAsync("run-1", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CancelSignupAsync_escapes_run_id_in_path()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));

        await client.CancelSignupAsync("run with spaces/and?weird", CancellationToken.None);

        var path = handler.LastRequest!.RequestUri!.PathAndQuery;
        Assert.StartsWith("/api/runs/", path);
        Assert.EndsWith("/signup", path);
        Assert.Contains("%2F", path);
        Assert.DoesNotContain(" ", path);
        Assert.DoesNotContain("?weird", path);
    }
}
