// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.FluentUI.AspNetCore.Components;

namespace Lfm.App.Services;

/// <summary>
/// Convenience wrapper around <see cref="IToastService"/> for common
/// success/error toast notifications.
/// </summary>
public sealed class ToastHelper(IToastService toastService)
{
    public void ShowSuccess(string message)
    {
        toastService.ShowSuccess(message);
    }

    public void ShowError(string message)
    {
        toastService.ShowError(message);
    }
}
