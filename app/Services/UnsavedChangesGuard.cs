// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Lfm.App.Services;

public sealed class UnsavedChangesGuard(NavigationManager navigation, IJSRuntime js) : IAsyncDisposable, IDisposable
{
    private readonly Dictionary<object, Registration> _registrations = new(ReferenceEqualityComparer.Instance);
    private IDisposable? _locationChangingRegistration;
    private IJSObjectReference? _module;
    private bool _beforeUnloadEnabled;
    private bool _disposed;
    private bool _suppressNextNavigation;

    public event Action? StateChanged;

    public bool IsConfirmationVisible { get; private set; }
    public string? PendingTargetLocation { get; private set; }

    public Registration Register(object owner, Func<bool> isDirty)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_registrations.Remove(owner, out var existing))
        {
            existing.Deactivate();
        }

        _locationChangingRegistration ??= navigation.RegisterLocationChangingHandler(OnLocationChangingAsync);
        var registration = new Registration(this, owner, isDirty);
        _registrations.Add(owner, registration);
        return registration;
    }

    public async Task RefreshAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await SetBeforeUnloadEnabledAsync(IsDirty());
    }

    public async Task StayAsync()
    {
        PendingTargetLocation = null;
        IsConfirmationVisible = false;
        NotifyStateChanged();
        await RefreshAsync();
    }

    public async Task ConfirmLeaveAsync()
    {
        var target = PendingTargetLocation;
        PendingTargetLocation = null;
        IsConfirmationVisible = false;
        ClearRegistrations();
        await SetBeforeUnloadEnabledAsync(false);
        NotifyStateChanged();

        if (!string.IsNullOrEmpty(target))
        {
            _suppressNextNavigation = true;
            navigation.NavigateTo(target);
        }
    }

    private async ValueTask OnLocationChangingAsync(LocationChangingContext context)
    {
        if (_suppressNextNavigation)
        {
            _suppressNextNavigation = false;
            return;
        }

        if (!IsInternalTarget(context.TargetLocation) || !IsDirty())
        {
            return;
        }

        context.PreventNavigation();
        PendingTargetLocation = context.TargetLocation;
        IsConfirmationVisible = true;
        NotifyStateChanged();
        await SetBeforeUnloadEnabledAsync(true);
    }

    private bool IsDirty() => _registrations.Values.Any(registration => registration.IsDirty());

    private bool IsInternalTarget(string targetLocation)
    {
        var target = navigation.ToAbsoluteUri(targetLocation);
        return target.AbsoluteUri.StartsWith(navigation.BaseUri, StringComparison.OrdinalIgnoreCase);
    }

    private async ValueTask UnregisterAsync(Registration registration)
    {
        if (_registrations.TryGetValue(registration.Owner, out var current)
            && ReferenceEquals(current, registration))
        {
            _registrations.Remove(registration.Owner);
        }

        registration.Deactivate();

        if (_registrations.Count == 0)
        {
            _locationChangingRegistration?.Dispose();
            _locationChangingRegistration = null;
            PendingTargetLocation = null;
            IsConfirmationVisible = false;
            NotifyStateChanged();
        }

        await SetBeforeUnloadEnabledAsync(IsDirty());
    }

    private void ClearRegistrations()
    {
        foreach (var registration in _registrations.Values)
        {
            registration.Deactivate();
        }

        _registrations.Clear();
        _locationChangingRegistration?.Dispose();
        _locationChangingRegistration = null;
    }

    private async ValueTask SetBeforeUnloadEnabledAsync(bool enabled)
    {
        if (_beforeUnloadEnabled == enabled)
        {
            return;
        }

        if (_module is null && !enabled)
        {
            _beforeUnloadEnabled = false;
            return;
        }

        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/unsavedChanges.js");
        await _module.InvokeVoidAsync("setEnabled", enabled);
        _beforeUnloadEnabled = enabled;
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        ClearRegistrations();
        await SetBeforeUnloadEnabledAsync(false);
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public sealed class Registration : IAsyncDisposable, IDisposable
    {
        private UnsavedChangesGuard? _guard;
        private readonly Func<bool> _isDirty;

        internal Registration(UnsavedChangesGuard guard, object owner, Func<bool> isDirty)
        {
            _guard = guard;
            Owner = owner;
            _isDirty = isDirty;
        }

        internal object Owner { get; }
        internal bool Active { get; private set; } = true;

        internal bool IsDirty() => Active && _isDirty();

        internal void Deactivate()
        {
            Active = false;
            _guard = null;
        }

        public async ValueTask DisposeAsync()
        {
            if (_guard is { } guard)
            {
                await guard.UnregisterAsync(this);
            }
        }

        public void Dispose() => _ = DisposeAsync().AsTask();
    }
}
