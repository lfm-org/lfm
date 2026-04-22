// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Text;
using Lfm.App.Services;
using Lfm.Contracts.Admin;
using Moq;
using Xunit;

namespace Lfm.App.Core.Tests.Services;

/// <summary>
/// Pins the NDJSON wire contract between the admin refresh function and the
/// admin refresh page: every non-terminal line is a <c>progress</c> event that
/// reaches <see cref="IProgress{T}"/>, and the final <c>done</c> line is
/// unwrapped into <see cref="WowReferenceRefreshResponse"/>.
/// </summary>
public class WowReferenceRefreshAdminClientTests
{
    private static WowReferenceAdminClient MakeClient(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7071/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("api-admin")).Returns(http);
        return new WowReferenceAdminClient(factory.Object);
    }

    private static StubHttpMessageHandler Ndjson(string body) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson"),
        });

    [Fact]
    public async Task RefreshAsync_reports_each_progress_line_to_the_caller_sink()
    {
        var body = string.Join('\n', [
            """{"type":"progress","entity":"instances","phase":"start","processed":0,"total":2}""",
            """{"type":"progress","entity":"instances","phase":"progress","processed":1,"total":2,"current":"Ara-Kara"}""",
            """{"type":"progress","entity":"instances","phase":"progress","processed":2,"total":2,"current":"Mists"}""",
            """{"type":"done","response":{"results":[{"name":"instances","status":"synced (2 docs)"}]}}""",
            "",
        ]);
        var client = MakeClient(Ndjson(body));

        var events = new List<WowReferenceRefreshProgress>();
        var progress = new Progress<WowReferenceRefreshProgress>(events.Add);

        var result = await client.RefreshAsync(CancellationToken.None, progress);

        // Progress<T> posts back via SynchronizationContext; inside a plain
        // unit test the callback runs on the thread pool, so allow a brief
        // flush before asserting.
        await Task.Delay(50);

        Assert.Collection(events,
            e =>
            {
                Assert.Equal("instances", e.Entity);
                Assert.Equal("start", e.Phase);
                Assert.Equal(2, e.Total);
            },
            e =>
            {
                Assert.Equal("progress", e.Phase);
                Assert.Equal(1, e.Processed);
                Assert.Equal("Ara-Kara", e.Current);
            },
            e =>
            {
                Assert.Equal(2, e.Processed);
                Assert.Equal("Mists", e.Current);
            });
        Assert.Single(result.Results);
        Assert.Equal("synced (2 docs)", result.Results[0].Status);
    }

    [Fact]
    public async Task RefreshAsync_returns_final_results_when_no_progress_sink_is_supplied()
    {
        var body = string.Join('\n', [
            """{"type":"progress","entity":"instances","phase":"progress","processed":1,"total":1,"current":"Ara-Kara"}""",
            """{"type":"done","response":{"results":[{"name":"instances","status":"synced (1 docs)"}]}}""",
            "",
        ]);
        var client = MakeClient(Ndjson(body));

        var result = await client.RefreshAsync(CancellationToken.None);

        Assert.Single(result.Results);
        Assert.Equal("instances", result.Results[0].Name);
    }

    [Fact]
    public async Task RefreshAsync_tolerates_empty_lines_in_the_stream()
    {
        // Whitespace-only lines can appear on the wire if an intermediate
        // writes a stray newline; the client must ignore them rather than fail
        // to parse.
        var body = "\n\n"
                   + """{"type":"done","response":{"results":[]}}"""
                   + "\n";
        var client = MakeClient(Ndjson(body));

        var result = await client.RefreshAsync(CancellationToken.None);

        Assert.Empty(result.Results);
    }
}
