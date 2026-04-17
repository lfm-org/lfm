// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.FluentUI.AspNetCore.Components;

namespace Lfm.App.Services;

public interface IThemeService
{
    DesignThemeModes Mode { get; }
    event Action? OnChange;
    void Toggle();
    void SetMode(DesignThemeModes mode);
}

public sealed class ThemeService : IThemeService
{
    public DesignThemeModes Mode { get; private set; } = DesignThemeModes.Dark;

    public event Action? OnChange;

    public void Toggle()
    {
        Mode = Mode == DesignThemeModes.Dark
            ? DesignThemeModes.Light
            : DesignThemeModes.Dark;
        OnChange?.Invoke();
    }

    public void SetMode(DesignThemeModes mode)
    {
        if (Mode != mode)
        {
            Mode = mode;
            OnChange?.Invoke();
        }
    }
}
