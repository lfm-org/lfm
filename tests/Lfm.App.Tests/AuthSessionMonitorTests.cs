// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Bunit;
using Bunit.TestDoubles;
using Lfm.App.Auth;
using Lfm.App.Components;
using Lfm.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lfm.App.Tests;

public class AuthSessionMonitorTests : ComponentTestBase
{
    [Fact]
    public void SessionExpired_with_cached_authenticated_session_redirects_to_login_and_clears_auth_state()
    {
        var notifier = new SessionExpiryNotifier();
        var refresher = new RecordingAuthStateRefresher(hasAuthenticatedSession: true);
        Services.AddSingleton<ISessionExpiryNotifier>(notifier);
        Services.AddSingleton<IAuthStateRefresher>(refresher);
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/runs/abc?tab=signups");
        Render<AuthSessionMonitor>();

        notifier.NotifySessionExpired();

        Assert.False(refresher.HasAuthenticatedSession);
        Assert.Equal(1, refresher.MarkExpiredCount);
        Assert.EndsWith("/login?redirect=%2Fruns%2Fabc%3Ftab%3Dsignups", nav.Uri);
        Assert.True(nav.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public void SessionExpired_without_cached_authenticated_session_does_not_redirect()
    {
        var notifier = new SessionExpiryNotifier();
        var refresher = new RecordingAuthStateRefresher(hasAuthenticatedSession: false);
        Services.AddSingleton<ISessionExpiryNotifier>(notifier);
        Services.AddSingleton<IAuthStateRefresher>(refresher);
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/");
        Render<AuthSessionMonitor>();

        notifier.NotifySessionExpired();

        Assert.Equal("http://localhost/", nav.Uri);
        Assert.Equal(0, refresher.MarkExpiredCount);
    }

    [Fact]
    public async Task Resume_probe_refreshes_auth_state_only_when_cached_session_exists()
    {
        var notifier = new SessionExpiryNotifier();
        var refresher = new RecordingAuthStateRefresher(hasAuthenticatedSession: true);
        Services.AddSingleton<ISessionExpiryNotifier>(notifier);
        Services.AddSingleton<IAuthStateRefresher>(refresher);
        var cut = Render<AuthSessionMonitor>();

        await cut.Instance.CheckSessionAsync();
        refresher.HasAuthenticatedSession = false;
        await cut.Instance.CheckSessionAsync();

        Assert.Equal(1, refresher.RefreshCount);
    }

    [Fact]
    public async Task Resume_probe_rate_limits_repeated_auth_state_refreshes()
    {
        var notifier = new SessionExpiryNotifier();
        var refresher = new RecordingAuthStateRefresher(hasAuthenticatedSession: true);
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        Services.AddSingleton<ISessionExpiryNotifier>(notifier);
        Services.AddSingleton<IAuthStateRefresher>(refresher);
        Services.AddSingleton<TimeProvider>(timeProvider);
        var cut = Render<AuthSessionMonitor>();

        await cut.Instance.CheckSessionAsync();
        await cut.Instance.CheckSessionAsync();
        timeProvider.Advance(TimeSpan.FromMinutes(4));
        await cut.Instance.CheckSessionAsync();
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await cut.Instance.CheckSessionAsync();

        Assert.Equal(2, refresher.RefreshCount);
    }

    [Fact]
    public async Task CredentialsHandler_notifies_session_expiry_on_401_response()
    {
        var notifier = new SessionExpiryNotifier();
        var notified = 0;
        notifier.SessionExpired += () => notified++;
        using var handler = new CredentialsHandler(notifier)
        {
            InnerHandler = new StaticResponseHandler(HttpStatusCode.Unauthorized),
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("http://localhost/api/v1/runs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, notified);
    }

    [Fact]
    public async Task CredentialsHandler_does_not_notify_session_expiry_on_403_response()
    {
        var notifier = new SessionExpiryNotifier();
        var notified = 0;
        notifier.SessionExpired += () => notified++;
        using var handler = new CredentialsHandler(notifier)
        {
            InnerHandler = new StaticResponseHandler(HttpStatusCode.Forbidden),
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("http://localhost/api/v1/guild/admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, notified);
    }

    private sealed class RecordingAuthStateRefresher(bool hasAuthenticatedSession) : IAuthStateRefresher
    {
        public bool HasAuthenticatedSession { get; set; } = hasAuthenticatedSession;
        public int RefreshCount { get; private set; }
        public int MarkExpiredCount { get; private set; }

        public void RefreshAuthenticationState() => RefreshCount++;

        public void MarkSessionExpired()
        {
            MarkExpiredCount++;
            HasAuthenticatedSession = false;
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }

    private sealed class StaticResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
