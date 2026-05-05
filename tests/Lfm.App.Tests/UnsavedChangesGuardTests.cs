// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using Lfm.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lfm.App.Tests;

public class UnsavedChangesGuardTests : ComponentTestBase
{
    private UnsavedChangesGuard CreateGuard()
    {
        return Services.GetRequiredService<UnsavedChangesGuard>();
    }

    [Fact]
    public async Task LocationChangingHandler_PreventsNavigation_WhenDirty()
    {
        JSInterop.SetupModule("./js/unsavedChanges.js");
        var guard = CreateGuard();
        await using var registration = guard.Register(this, () => true);
        await guard.RefreshAsync();
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        nav.NavigateTo("/runs");

        Assert.True(guard.IsConfirmationVisible);
        Assert.Equal("/runs", new Uri(guard.PendingTargetLocation!).AbsolutePath);
        var entry = Assert.Single(nav.History);
        Assert.Equal(NavigationState.Prevented, entry.State);
    }

    [Fact]
    public async Task LocationChangingHandler_AllowsNavigation_WhenClean()
    {
        JSInterop.SetupModule("./js/unsavedChanges.js");
        var guard = CreateGuard();
        await using var registration = guard.Register(this, () => false);
        await guard.RefreshAsync();
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        nav.NavigateTo("/runs");

        Assert.False(guard.IsConfirmationVisible);
        Assert.Equal("/runs", new Uri(nav.Uri).AbsolutePath);
        var entry = Assert.Single(nav.History);
        Assert.Equal(NavigationState.Succeeded, entry.State);
    }

    [Fact]
    public async Task RefreshAsync_Toggles_BeforeUnload_WhenDirtyStateChanges()
    {
        var module = JSInterop.SetupModule("./js/unsavedChanges.js");
        var dirty = false;
        var guard = CreateGuard();
        await using var registration = guard.Register(this, () => dirty);

        await guard.RefreshAsync();
        dirty = true;
        await guard.RefreshAsync();
        dirty = false;
        await guard.RefreshAsync();

        module.VerifyInvoke("setEnabled", 2);
        Assert.Equal(true, module.Invocations["setEnabled"][0].Arguments[0]);
        Assert.Equal(false, module.Invocations["setEnabled"][1].Arguments[0]);
    }

    [Fact]
    public async Task Stay_ClosesConfirmation_WithoutContinuingNavigation()
    {
        JSInterop.SetupModule("./js/unsavedChanges.js");
        var guard = CreateGuard();
        await using var registration = guard.Register(this, () => true);
        await guard.RefreshAsync();
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        var startPath = new Uri(nav.Uri).AbsolutePath;

        nav.NavigateTo("/runs");
        await guard.StayAsync();

        Assert.False(guard.IsConfirmationVisible);
        Assert.Null(guard.PendingTargetLocation);
        Assert.Equal(startPath, new Uri(nav.Uri).AbsolutePath);
        Assert.Equal(NavigationState.Prevented, Assert.Single(nav.History).State);
    }

    [Fact]
    public async Task ConfirmLeave_ClearsDirtyState_AndContinuesOriginalNavigation()
    {
        var module = JSInterop.SetupModule("./js/unsavedChanges.js");
        var guard = CreateGuard();
        await using var registration = guard.Register(this, () => true);
        await guard.RefreshAsync();
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        nav.NavigateTo("/runs");
        await guard.ConfirmLeaveAsync();

        Assert.False(guard.IsConfirmationVisible);
        Assert.Null(guard.PendingTargetLocation);
        Assert.Equal("/runs", new Uri(nav.Uri).AbsolutePath);
        Assert.Equal(NavigationState.Succeeded, nav.History.First().State);
        Assert.Equal(false, module.Invocations["setEnabled"].Last().Arguments[0]);
    }

    [Fact]
    public async Task ConfirmLeave_DoesNotSuppressNextDirtyRegistration()
    {
        JSInterop.SetupModule("./js/unsavedChanges.js");
        var guard = CreateGuard();
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        var ownerA = new object();
        var ownerB = new object();

        await using var registrationA = guard.Register(ownerA, () => true);
        await guard.RefreshAsync();

        nav.NavigateTo("/runs");

        Assert.True(guard.IsConfirmationVisible);
        Assert.Equal(NavigationState.Prevented, nav.History.First().State);

        await guard.ConfirmLeaveAsync();

        Assert.False(guard.IsConfirmationVisible);
        Assert.Equal("/runs", new Uri(nav.Uri).AbsolutePath);
        Assert.Equal(NavigationState.Succeeded, nav.History.First().State);

        await using var registrationB = guard.Register(ownerB, () => true);
        await guard.RefreshAsync();

        nav.NavigateTo("/guild");

        Assert.True(guard.IsConfirmationVisible);
        Assert.Equal("/guild", new Uri(guard.PendingTargetLocation!).AbsolutePath);
        Assert.Equal(NavigationState.Prevented, nav.History.First().State);
    }
}
