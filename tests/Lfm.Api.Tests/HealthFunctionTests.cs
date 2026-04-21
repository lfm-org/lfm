// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Lfm.Api.Functions;
using Lfm.Api.Options;
using Lfm.Contracts.Health;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using MsOptions = Microsoft.Extensions.Options.Options;
using Moq;
using Xunit;

namespace Lfm.Api.Tests;

public class HealthFunctionTests
{
    [Fact]
    public void Health_returns_ok_status_and_timestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var result = HealthFunction.Build();
        var after = DateTimeOffset.UtcNow;

        Assert.IsType<HealthResponse>(result);
        Assert.Equal("ok", result.Status);
        Assert.True(result.Timestamp >= before);
        Assert.True(result.Timestamp <= after);
    }

    [Fact]
    public void Live_returns_ok_with_health_response_body()
    {
        var (fn, _) = CreateReadyFunction();

        var before = DateTimeOffset.UtcNow;
        var result = fn.Live(new DefaultHttpContext().Request);
        var after = DateTimeOffset.UtcNow;

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var body = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("ok", body.Status);
        Assert.True(body.Timestamp >= before);
        Assert.True(body.Timestamp <= after);
    }

    private static (HealthFunction fn, Mock<Database> mockDb) CreateReadyFunction()
    {
        var mockDb = new Mock<Database>();
        var mockClient = new Mock<CosmosClient>();
        mockClient.Setup(c => c.GetDatabase("test-db")).Returns(mockDb.Object);

        var opts = MsOptions.Create(new CosmosOptions
        {
            Endpoint = "https://test.documents.azure.com",
            DatabaseName = "test-db",
        });

        return (new HealthFunction(mockClient.Object, opts, new TestLogger<HealthFunction>()), mockDb);
    }

    [Fact]
    public async Task Ready_returns_ok_with_ready_status_when_cosmos_is_reachable()
    {
        var (fn, mockDb) = CreateReadyFunction();
        mockDb.Setup(d => d.ReadAsync(It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DatabaseResponse)null!);

        var before = DateTimeOffset.UtcNow;
        var result = await fn.Ready(new DefaultHttpContext().Request, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var body = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("ready", body.Status);
        Assert.True(body.Timestamp >= before);
        Assert.True(body.Timestamp <= after);

        mockDb.Verify(d => d.ReadAsync(It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ready_returns_503_with_unready_status_when_cosmos_throws()
    {
        // Per HealthFunction.Ready xmldoc: failure must surface as 503 with status="unready"
        // and nothing else. The exception is logged server-side; clients key off status only.
        var (fn, mockDb) = CreateReadyFunction();
        mockDb.Setup(d => d.ReadAsync(It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Service unavailable", System.Net.HttpStatusCode.ServiceUnavailable, 0, "", 0));

        var result = await fn.Ready(new DefaultHttpContext().Request, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
        var statusProp = obj.Value!.GetType().GetProperty("status")!.GetValue(obj.Value);
        Assert.Equal("unready", statusProp);
        Assert.Null(obj.Value!.GetType().GetProperty("error"));
    }

    [Fact]
    public async Task Ready_returns_503_with_unready_status_for_unexpected_errors()
    {
        var (fn, mockDb) = CreateReadyFunction();
        mockDb.Setup(d => d.ReadAsync(It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection pool exhausted"));

        var result = await fn.Ready(new DefaultHttpContext().Request, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
        var statusProp = obj.Value!.GetType().GetProperty("status")!.GetValue(obj.Value);
        Assert.Equal("unready", statusProp);
    }

    // ------------------------------------------------------------------
    // Health endpoints must not leak sensitive configuration in their
    // response bodies. Replaces the deleted SecuritySpec.cs E2E test
    // `HealthEndpoint_NoSensitiveInfo`, which exercised the full Docker
    // stack to prove the same property at much higher cost. Asserts on
    // the JSON-serialized form of every health response shape (live,
    // ready-success, ready-cosmos-error, ready-unexpected-error) since a
    // future refactor that re-adds a response field carrying `ex.Message`
    // or `ex.ToString()` would be a real regression.
    // ------------------------------------------------------------------

    private static readonly string[] SensitiveSubstrings =
    [
        "AccountKey",
        "AccountEndpoint",
        "connectionString",
        "password",
        "secret",
        "ClientSecret",
    ];

    private static void AssertNoSensitiveSubstrings(string body, string context)
    {
        foreach (var marker in SensitiveSubstrings)
        {
            Assert.DoesNotContain(marker, body);
        }
    }

    [Fact]
    public void Live_response_body_does_not_leak_sensitive_configuration()
    {
        var (fn, _) = CreateReadyFunction();

        var result = fn.Live(new DefaultHttpContext().Request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        AssertNoSensitiveSubstrings(json, "/api/health");
    }

    [Fact]
    public async Task Ready_success_response_body_does_not_leak_sensitive_configuration()
    {
        var (fn, mockDb) = CreateReadyFunction();
        mockDb.Setup(d => d.ReadAsync(It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DatabaseResponse)null!);

        var result = await fn.Ready(new DefaultHttpContext().Request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        AssertNoSensitiveSubstrings(json, "/api/health/ready (success)");
    }

    [Fact]
    public async Task Ready_failure_response_body_does_not_leak_sensitive_configuration()
    {
        // CosmosException messages typically contain the account endpoint and
        // diagnostic JSON. The contract is that the readiness response body
        // carries only { status: "unready" } — no exception type, message, or
        // any derived string. Pin it: a refactor that re-adds an `error` field
        // (or anything else derived from the exception) would surface
        // AccountEndpoint and request diagnostics to clients.
        var (fn, mockDb) = CreateReadyFunction();
        mockDb.Setup(d => d.ReadAsync(It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException(
                "Service unavailable. AccountEndpoint=https://test.documents.azure.com; AccountKey=topsecret",
                System.Net.HttpStatusCode.ServiceUnavailable, 0, "", 0));

        var result = await fn.Ready(new DefaultHttpContext().Request, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        var json = JsonSerializer.Serialize(obj.Value);
        AssertNoSensitiveSubstrings(json, "/api/health/ready (failure)");
        Assert.DoesNotContain("CosmosException", json);
        Assert.DoesNotContain("Service unavailable", json);
    }
}
