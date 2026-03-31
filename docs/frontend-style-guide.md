# Frontend Style Guide

Authoritative reference for AI agents building UI in this codebase. All patterns are derived from existing code — follow them exactly.

## Theme

- **Dark mode only.** Background `#121212`, paper `#1d1d1d`. No light mode.
- Custom tokens are exported from `frontend/src/theme.ts`:
  - `attendance` — status colors for raid signups (in/out/bench/late/away/unknown)
  - `surface` — subtle rgba overlays for decorative cards (`tint`, `tintStrong`, `border`, `borderSubtle`)
  - `layout` — spacing constants (`maxWidth: 1100`, `px: 2`, `py: 3`, `pageGap: 3`, `componentGap: 2`)
- System font stack (Segoe UI, Roboto, etc.). Buttons: weight 600, no text transform.
- CSS variables enabled (`cssVariables: true`).
- MUI locale adapts to language (Finnish via `muiFiFI`).

## Styling Method

- **`sx` prop only.** No styled-components, CSS modules, or external stylesheets.
- When a component accepts `sx` from the caller, merge with array syntax:

```tsx
sx={[
  { /* base styles */ },
  ...(Array.isArray(sx) ? sx : sx ? [sx] : []),
]}
```

## Layout

- **Pages:** wrap content in `PageContainer` (auto max-width, responsive padding).
- **Cards:** use `SurfaceCard` (outlined Paper, `elevation={0}`, border-radius 2, padding 2). Supports `tone="error"`.
- **Vertical stacks:** `<Stack spacing={2}>` or `<Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>`.
- **Grids:** CSS grid via `sx`, not MUI Grid component:

```tsx
sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(3, 1fr)" }, gap: 2 }}
```

- Use `layout.pageGap` between top-level sections, `layout.componentGap` within components.

## Typography

| Purpose | Variant | Component prop | Weight |
|---------|---------|----------------|--------|
| Page title | `h5` | `component="h1"` | default |
| Section heading | `h6` | `component="h2"` | 700 |
| Body text | `body1` | — | default |
| Secondary text | `body2` | — | default |
| Metadata / helpers | `caption` | — | default |

- Section headings often use `textTransform: "uppercase"` and `letterSpacing: "0.05em"`.
- Always set `component` to maintain semantic heading hierarchy for accessibility.

## Colors

- **Always use semantic palette tokens**, never hardcoded hex:
  - `color="text.primary"`, `color="text.secondary"`, `color="text.disabled"`
  - `bgcolor="background.paper"`, `bgcolor="background.default"`
  - `borderColor="divider"`
  - `color="error"`, `color="primary.main"`
- **Exceptions:** WoW class colors (`classColors.ts`) and attendance statuses (`theme.ts`) are domain-specific constants.
- Surface overlays use the `surface` token from `theme.ts`.

## Responsive Design

- **Mobile-first.** Default styles target `xs`, override at `md`.
- Responsive values via object syntax:

```tsx
direction={{ xs: "column", sm: "row" }}
py: { xs: layout.py, md: layout.py + 2 }
```

- `useMediaQuery` for conditional rendering:

```tsx
const theme = useTheme();
const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
```

## Forms

- `TextField` with `fullWidth`, `label={t("key")}`, `error`, `helperText`.
- Selects: `FormControl` + `InputLabel` + `Select` + `FormHelperText` for errors.
- Date/time: `DateTimePicker` with `slotProps.textField` for error/helper props.
- Toggle groups: `ToggleButtonGroup` with `exclusive`, `size="small"`.
- Spacing between fields: `sx={{ mb: 2 }}`.

## Loading & Empty States

- **Loading:** `<CircularProgress />` (default size) or `<CircularProgress size={20} />` (inline).
- **Empty data:** italic disabled text:

```tsx
<Typography variant="body2" color="text.disabled" sx={{ fontStyle: "italic" }}>
  {t("key.empty")}
</Typography>
```

- **Errors:** `<Alert severity="error" sx={{ mb: 2 }}>{message}</Alert>`.

## Icons

- **No icon library.** Use emoji (e.g. flag emoji for language switcher) or text/Avatar fallbacks.
- Do not add Material Icons or any other icon package.

## i18n

- All user-visible text goes through `t()` from `react-i18next`.
- Keys use nested dot notation: `"raidList.signups"`, `"nav.logo"`.
- Interpolation: `t("key", { count: n })` with `{{ count }}` in locale files.
- Locale files: `frontend/src/i18n/locales/{en,fi}.json`.

## Accessibility

- Menus: `aria-controls`, `aria-expanded`, `aria-haspopup`, `aria-labelledby`.
- Regions: `role="region"` with `aria-label`.
- Toggle/icon-only buttons: always add `aria-label`.
- Heading hierarchy: use `component="h1"` / `component="h2"` on Typography to match semantic level.
- Skip link exists for keyboard navigation — do not remove.

## Component Conventions

- **Default exports** for all components.
- Props interfaces extend MUI base types (`PaperProps`, `BoxProps`, etc.) plus custom fields.
- Components that accept `sx` must merge it using the array pattern shown above.
- No `React.FC` — use plain function declarations.
- Hooks: `useTheme`, `useMediaQuery`, `useTranslation` imported at component top.

## Animation

- Minimal. Card hover uses `transition: "background-color 0.15s"`.
- Hover states: `"&:hover": { bgcolor: "action.hover" }`.
- No animation libraries (framer-motion, etc.).
