# Frontend Style Guide

Authoritative reference for AI agents building UI in this codebase. All patterns are derived from existing code — follow them exactly.

## Theme

- **Light and dark mode.** Default is dark. User toggles via navbar button; persisted in localStorage (`lfm-theme`).
- FluentUI Blazor's `FluentDesignTheme` component wraps the app in `App.razor`. It sets CSS custom properties (design tokens) for the active theme.
- All color-related styling must use FluentUI CSS variables so both themes work. Never hardcode hex colors, `rgb()`, or named colors (`white`, `red`) in styles.
- WoW class colors are canonical Blizzard-defined constants and must not be changed. When contrast is insufficient against the current theme background, use a structural fix (e.g., a subtle contrasting background chip behind the colored text) rather than altering the color values.
- System font stack via FluentUI defaults. No custom fonts.

### Common FluentUI CSS variables

| Variable | Purpose |
|----------|---------|
| `--neutral-layer-1` | Page background |
| `--neutral-layer-3` | Elevated surface |
| `--neutral-layer-4` | Header/footer background |
| `--neutral-fill-rest` | Card/container fill |
| `--neutral-stroke-rest` | Borders, dividers |
| `--neutral-foreground-rest` | Primary text |
| `--neutral-foreground-hint-rest` | Secondary/muted text |
| `--accent-fill-rest` | Primary accent (selected items, CTAs) |
| `--foreground-on-accent-rest` | Text on accent backgrounds |
| `--error` | Error state color |

## Styling Method

- **Inline styles with FluentUI CSS variables** for color-related properties:
  ```html
  <div style="background:var(--neutral-layer-4);border:1px solid var(--neutral-stroke-rest)">
  ```
- **Inline styles** for layout (padding, margin, flex, grid). These are theme-independent.
- **app.css** for global utilities (skip-link, mobile nav, loading spinner). Keep minimal.
- **No component-scoped .razor.css files** — use inline styles. The old `MainLayout.razor.css` was dead Blazor template CSS and has been removed.
- **No Bootstrap.** FluentUI handles all component styling. Bootstrap was removed.

## Layout

- **Pages:** content lives inside `MainLayout`'s `<main>` which provides `padding: 24px 16px`.
- **Stacks:** `<FluentStack Orientation="..." HorizontalGap="N" VerticalGap="N">` for flex layouts.
- **Cards:** `<FluentCard>` for content grouping.
- **Grids:** CSS grid via inline `style` for responsive card layouts:
  ```html
  <div style="display:grid;gap:12px;grid-template-columns:repeat(auto-fill,minmax(min(260px,100%),1fr))">
  ```
- **Responsive:** at mobile widths (<768px), horizontal stacks should have a vertical fallback. Use CSS media queries in `app.css` for layout shifts, not inline styles.
- **DataGrids on mobile:** wrap `<FluentDataGrid>` in a `<div style="overflow-x:auto">` for horizontal scroll.

## Typography

FluentUI Blazor handles typography via `<FluentLabel>`:

| Purpose | Component |
|---------|-----------|
| Page title | `<FluentLabel Typo="Typography.H3">` |
| Section heading | `<FluentLabel Typo="Typography.H4">` |
| Body text | `<FluentLabel Typo="Typography.Body">` |
| Secondary text | `<FluentLabel>` with `color:var(--neutral-foreground-hint-rest)` |
| Page browser title | `<PageTitle>@Loc["page.title"] — @Loc["nav.logo"]</PageTitle>` |

## Colors

- **Always use FluentUI CSS variables**, never hardcoded hex:
  - Text: `var(--neutral-foreground-rest)`, `var(--neutral-foreground-hint-rest)`
  - Backgrounds: `var(--neutral-layer-1)`, `var(--neutral-fill-rest)`
  - Borders: `var(--neutral-stroke-rest)`
  - Accent: `var(--accent-fill-rest)`, `var(--foreground-on-accent-rest)`
  - Error: `var(--error)`
- **Exception:** WoW class colors are Blizzard-defined constants (never change them). Fix contrast structurally — e.g., background chip behind colored text.

## Components

### Containers
- `<FluentCard>` — content card
- `<FluentStack>` — flex layout with gap control

### Buttons
- `<FluentButton Appearance="Appearance.Accent">` — primary CTA
- `<FluentButton Appearance="Appearance.Outline">` — secondary action
- `<FluentButton Appearance="Appearance.Stealth">` — icon/subtle buttons
- Touch targets: minimum 44px height for mobile accessibility

### Forms
- `<FluentTextField @bind-Value="field">` — text input
- `<FluentSelect @bind-Value="field">` with `<FluentOption>` — dropdowns
- `<FluentTextArea @bind-Value="field">` — multiline
- Form containers: `max-width: 600px` for create forms, `max-width: 800px` for edit forms
- All inputs should be full-width within their container

### Data
- `<FluentDataGrid Items="@data.AsQueryable()" TGridItem="TDto">` with:
  - `<PropertyColumn>` for simple property binding
  - `<TemplateColumn>` for custom cell rendering
  - `GridTemplateColumns` for column sizing

### Feedback
- `<FluentProgressRing />` — loading state
- `<FluentMessageBar Intent="MessageIntent.Error">` — inline errors
- `Toast.ShowSuccess()` / `Toast.ShowError()` — transient notifications

## State Management

- **No global store.** State lives in DI services + component-local fields.
- **`LoadingState<T>`** discriminated union for async data:
  ```csharp
  @switch (state)
  {
      case LoadingState<T>.Loading:
          <FluentProgressRing />
          break;
      case LoadingState<T>.Failure f:
          <FluentMessageBar Intent="MessageIntent.Error">@f.Message</FluentMessageBar>
          break;
      case LoadingState<T>.Success s:
          // render s.Value
          break;
  }
  ```
- **Event-driven updates:** services fire `Action? OnChange` events; components call `InvokeAsync(StateHasChanged)` in handlers.
- **IDisposable:** always unsubscribe from events in `Dispose()`.

## Service Lifetimes

| Lifetime | Services |
|----------|----------|
| Singleton | `IThemeService`, `ILocaleService`, `IStringLocalizer`, `IDataCache` |
| Scoped | API clients (`IRunsClient`, `IGuildClient`, etc.), `ToastHelper` |

## i18n

- Inject `@inject IStringLocalizer Loc` in every component with user-visible text.
- **All user-visible text** must go through `@Loc["key"]`. No hardcoded English strings.
- Format strings: `@Loc["key", arg1, arg2]` for keys with `{0}`, `{1}` placeholders.
- Toast messages: `Toast.ShowSuccess(Loc["key"])`, not hardcoded strings.
- Locale files: `app/wwwroot/locales/{en,fi}.json` with flat dot-notation keys.
- Locale switching: `ILocaleService.SetLocale("fi")` fires `OnLocaleChanged` event.

## Accessibility

- Skip-to-content link in MainLayout (`<a class="skip-link">`). CSS hides it until focused.
- `aria-label` on all icon-only and stealth buttons.
- `<PageTitle>` on every page for screen readers and browser tabs.
- Semantic heading hierarchy via FluentLabel Typo variants.
- Touch targets: minimum 44px for all interactive elements on mobile.

## Mobile / Responsive

- **Breakpoint: 768px.** Below this, desktop nav hides and mobile hamburger menu shows.
- CSS media queries in `app.css` handle the nav transition.
- Pages should work at 375px (iPhone SE). Test: no horizontal overflow, readable text, tappable buttons.
- Horizontal `FluentStack` layouts that contain multiple items should stack vertically on mobile via CSS or conditional rendering.
- Data tables: always wrap in `overflow-x: auto` container.

## File Organization

```
app/
  App.razor              — Root: FluentDesignTheme + Router
  Program.cs             — Service registration + locale bootstrap
  _Imports.razor         — Global using directives
  Layout/
    MainLayout.razor     — Header, footer, nav, theme toggle
  Pages/
    *.razor              — One file per route
  Components/
    *.razor              — Reusable UI components
  Services/
    *.cs                 — DI services (clients, theme, toast)
  i18n/
    *.cs                 — Locale service + JSON localizer
  Auth/
    *.cs                 — AuthenticationStateProvider
  wwwroot/
    css/app.css          — Global CSS (minimal)
    locales/{en,fi}.json — Translation files
    index.html           — Shell HTML
```
