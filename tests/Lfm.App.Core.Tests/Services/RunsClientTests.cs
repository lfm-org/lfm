using System.Net;
using FluentAssertions;
using Lfm.App.Services;
using Lfm.Contracts.Runs;
using Moq;
using Xunit;

namespace Lfm.App.Core.Tests.Services;

public class RunsClientTests
{
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
            StartTime: "2026-05-01T20:00:00Z",
            SignupCloseTime: "2026-05-01T18:00:00Z",
            Description: "Test run",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            CreatorGuild: "Stormchasers",
            CreatorGuildId: 42,
            InstanceId: 1,
            InstanceName: "Liberation of Undermine",
            CreatorBattleNetId: "player#1234",
            CreatedAt: "2026-04-01T10:00:00Z",
            Ttl: 604800,
            RunCharacters: []);

    private static RunDetailDto MakeDetail(string id = "run-1") =>
        new(
            Id: id,
            StartTime: "2026-05-01T20:00:00Z",
            SignupCloseTime: "2026-05-01T18:00:00Z",
            Description: "Test run",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            CreatorGuild: "Stormchasers",
            CreatorGuildId: 42,
            InstanceId: 1,
            InstanceName: "Liberation of Undermine",
            CreatorBattleNetId: "player#1234",
            CreatedAt: "2026-04-01T10:00:00Z",
            Ttl: 604800,
            RunCharacters: []);

    private static CreateRunRequest MakeCreateRequest() =>
        new(
            StartTime: "2026-05-01T20:00:00Z",
            SignupCloseTime: "2026-05-01T18:00:00Z",
            Description: "desc",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            InstanceId: 1,
            InstanceName: "Liberation of Undermine");

    [Fact]
    public async Task ListAsync_deserializes_json_array_response()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(
            HttpStatusCode.OK,
            new[] { MakeSummary("run-a"), MakeSummary("run-b") }));

        var result = await client.ListAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("run-a");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/runs");
    }

    [Fact]
    public async Task ListAsync_returns_empty_list_when_body_is_empty_array()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, Array.Empty<RunSummaryDto>()));

        var result = await client.ListAsync(CancellationToken.None);

        result.Should().BeEmpty();
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

        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetAsync_escapes_run_id_in_path()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));

        await client.GetAsync("run with spaces/and?weird", CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should()
            .StartWith("/api/runs/")
            .And.Contain("%2F")
            .And.NotContain(" ")
            .And.NotContain("?weird");
    }

    [Fact]
    public async Task GetAsync_throws_on_404_not_found()
    {
        // Pin current contract: GetFromJsonAsync<T> throws HttpRequestException on
        // non-2xx responses. If a future refactor wants to swallow 404s and return
        // null, this test must be updated deliberately.
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NotFound));

        var act = () => client.GetAsync("missing-run", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateAsync_posts_request_body_as_json()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));
        var request = MakeCreateRequest();

        var result = await client.CreateAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/runs");
        handler.LastRequest.Content.Should().NotBeNull();
        handler.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task CreateAsync_returns_null_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.BadRequest));
        var request = MakeCreateRequest();

        var result = await client.CreateAsync(request, CancellationToken.None);

        result.Should().BeNull("non-2xx responses must surface as a null result, not an exception");
    }

    [Fact]
    public async Task DeleteAsync_returns_true_on_success()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NoContent));

        var result = await client.DeleteAsync("run-1", CancellationToken.None);

        result.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public async Task DeleteAsync_returns_false_on_failure()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.Forbidden));

        var result = await client.DeleteAsync("run-1", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SignupAsync_posts_to_signup_subpath()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));
        var request = new SignupRequest(CharacterId: "char-1", DesiredAttendance: "IN", SpecId: null);

        var result = await client.SignupAsync("run-1", request, CancellationToken.None);

        result.Should().NotBeNull("a 2xx response must surface as a non-null detail, not be swallowed");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/runs/run-1/signup");
    }

    [Fact]
    public async Task UpdateAsync_puts_request_body_to_run_path()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));
        var request = new UpdateRunRequest(
            StartTime: "2026-05-01T20:00:00Z",
            SignupCloseTime: "2026-05-01T18:00:00Z",
            Description: "updated",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            InstanceId: 1,
            InstanceName: "Liberation of Undermine");

        var result = await client.UpdateAsync("run-1", request, CancellationToken.None);

        result.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/runs/run-1");
        handler.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task UpdateAsync_returns_null_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.BadRequest));
        var request = new UpdateRunRequest(null, null, null, null, null, null, null);

        var result = await client.UpdateAsync("run-1", request, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CancelSignupAsync_deletes_signup_subpath()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));

        var result = await client.CancelSignupAsync("run-1", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be("run-1");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/runs/run-1/signup");
    }

    [Fact]
    public async Task CancelSignupAsync_returns_null_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NotFound));

        var result = await client.CancelSignupAsync("run-1", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CancelSignupAsync_escapes_run_id_in_path()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeDetail("run-1")));

        await client.CancelSignupAsync("run with spaces/and?weird", CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.Should()
            .StartWith("/api/runs/")
            .And.EndWith("/signup")
            .And.Contain("%2F")
            .And.NotContain(" ")
            .And.NotContain("?weird");
    }
}
