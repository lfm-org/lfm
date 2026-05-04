# Frontend Style Guide

This is the product style guide for the LFM Blazor frontend. Treat it like a
small Figma design-system note: it defines visual language, token usage,
component appearance, density, and domain-specific color treatment.

It is not the responsive, accessibility, internationalization, performance,
state-management, or service-architecture guide. Use
`souroldgeezer-design:responsive-design` for responsive UI, WCAG, RTL,
text-expansion, forced-colors, focus behavior, viewport/container rules, and
Core Web Vitals guidance. Use `CLAUDE.md` for repository workflow and
architecture rules.

## Current Responsive And Locale Boundary

The current verified product scope is `en` and `fi` in left-to-right layouts.
Responsive, accessibility, and i18n readiness claims in this repository apply
to that scope unless a PR includes explicit readiness evidence for another
locale.

Any new locale file under `app/wwwroot/locales/` must include a matching
`docs/locale-readiness/<locale>.md` note before merge. The note must cover:

- Responsive sweep across phone, tablet, desktop, zoom, and reduced-width
  states.
- Text expansion for labels, navigation, form controls, cards, tables, and
  operational run rows.
- Locale parity for keys, fallback behavior, date/time/number formatting, and
  validation/error strings.
- Bidi/RTL behavior when the locale is right-to-left, or an explicit statement
  that the locale is left-to-right.
- Collation and sorting expectations for localized lists.

`scripts/check-locale-readiness.sh` enforces the evidence file for any locale
outside the current `en`/`fi` LTR scope.

## Design Character

- **Operational, not decorative.** LFM is a raid and guild operations tool.
  Screens should feel quiet, dense, and scan-friendly rather than promotional.
- **Domain color as signal.** World of Warcraft class, difficulty, attendance,
  and run-kind colors are useful when they identify game concepts. Keep them
  localized to chips, side accents, badges, and compact status markers.
- **Fluent first.** Microsoft Fluent UI Blazor provides the base component
  language, typography, theme tokens, and interaction styling.
- **Token-driven surfaces.** Page chrome, cards, text, borders, and selected
  states use Fluent CSS custom properties. Avoid one-off palettes.
- **Stable state styling.** Selection, hover, loading, and disabled states
  should not change text metrics or make rows/cards visually jump.

## Theme

The app supports dark and light themes through Fluent UI Blazor. Dark is the
default. `FluentDesignTheme` wraps the app in `App.razor` and persists the
theme with the `lfm-theme` storage key.

Use Fluent token roles for application color:

| Role | Token |
| --- | --- |
| Page background | `--neutral-layer-1` |
| Raised surface | `--neutral-layer-3` |
| Header/footer surface | `--neutral-layer-4` |
| Card or row fill | `--neutral-fill-rest` |
| Hover fill | `--neutral-fill-hover` |
| Selected neutral fill | `--neutral-fill-secondary-rest` |
| Divider or border | `--neutral-stroke-rest` |
| Primary text | `--neutral-foreground-rest` |
| Secondary text | `--neutral-foreground-hint-rest` |
| Primary action / selected accent | `--accent-fill-rest` |
| Text on accent fill | `--foreground-on-accent-rest` |
| Error emphasis | `--error` |

Use `--foreground-on-accent-rest` for text placed on `--accent-fill-rest`.
Do not use `--accent-foreground-rest` on an accent-filled surface; that token
is for accent-colored text or icons on a neutral surface.

## Typography

Use semantic HTML for document structure. Route titles and section titles must
render as the appropriate native heading level (`<h1>`, `<h2>`, `<h3>`, and so
on). Fluent typography is the visual treatment, not the proof of heading
semantics. If a Fluent component is used for a heading, bUnit coverage must prove
the rendered markup contains the intended heading element.

| Purpose | Semantic element and visual style |
| --- | --- |
| Route title | One rendered `<h1>` per routable page, styled as `Typography.H1` or equivalent |
| Product mark / compact page brand | Non-heading text unless it is the page title; style like `Typography.H3` |
| Card or section title | Native heading at the next valid level, styled like `Typography.H4` or `Typography.H5` |
| Body copy | `Typography.Body` or default `FluentLabel` |
| Metadata / helper text | `--neutral-foreground-hint-rest`, usually `0.85em` to `0.9em` |
| Numeric role counts | `font-variant-numeric: tabular-nums` |

Use the Fluent/system font stack. Do not add custom web fonts for product UI.

## Color Vocabulary

### Neutral UI

- Page background: `--neutral-layer-1`.
- App chrome: `--neutral-layer-4` with `--neutral-stroke-rest` separators.
- Cards, rows, role slots, and subtle chips: `--neutral-fill-rest`.
- Selected list rows: `--neutral-fill-secondary-rest` plus a stronger structural
  marker, such as a side stripe.
- Secondary text, timestamps, metadata, and helper copy:
  `--neutral-foreground-hint-rest`.

### Accent

Use `--accent-fill-rest` for:

- The primary action on a page or card.
- Selected segmented-control options.
- Selection outlines where the component does not already provide an
  appropriate Fluent treatment.
- Links and compact accent text on neutral surfaces.

Avoid using the accent color as generic decoration.

### World of Warcraft Domain Colors

These colors are domain constants. Do not tune them to fit a page palette.
Apply them through named helpers, CSS classes, or shared domain functions rather
than scattering literals through Razor markup.

Domain colors must not be the only source of meaning. Pair them with text,
shape, position, iconography, or component structure so the cue survives low
contrast, color-vision differences, and `forced-colors: active`. When a
brand-canonical WoW color cannot meet contrast as a meaningful UI boundary,
treat it as decorative reinforcement and make the actual meaning available
through text or another non-color cue.

| Domain cue | Visual treatment | Required non-color cue |
| --- | --- | --- |
| Character class | `WowClasses.GetColor(classId)` as a row/card side accent or compact text chip | Character name plus class/spec text |
| Dungeon run kind | `#0070dd` side accent | Run-kind label or role-composition context |
| Raid run kind | `#1eff00` side accent | Run-kind label or role-composition context |
| Mythic / Mythic+ difficulty | `#ff8000` filled difficulty pill | Difficulty label inside the pill |
| Heroic difficulty | `#a335ee` filled difficulty pill | Difficulty label inside the pill |
| Normal / LFR difficulty | Neutral outline pill | Difficulty label inside the pill |
| Signed-up marker | Compact star/glyph in the run row metadata area | Signup text or status metadata in the detail pane |

### Attendance Status

Attendance status is shown as a filled rounded pill. Keep labels short and use
the existing status vocabulary.

The status label is mandatory; the fill color is supporting evidence. Do not
render attendance as a color-only dot or border. In forced-colors mode, the pill
must keep a visible boundary and readable label using system colors or inherited
Fluent contrast pairs.

| Status | Fill |
| --- | --- |
| In | `#1d8049` |
| Late | `#a05900` |
| Bench | `#6c757d` |
| Out | `#c0392b` |
| Away | `#ad4400` |
| Unknown | Neutral fill and neutral text |

## Spacing And Density

Prefer compact, repeated spacing over large editorial gaps.

| Use | Value |
| --- | --- |
| Inline icon/text gap | `4px` |
| Compact stack gap | `8px` |
| Standard stack gap | `12px` |
| Section/card group gap | `16px` |
| Page content block padding | `24px` from `MainLayout` |
| Page content inline padding | `16px` from `MainLayout` |
| Pill inline padding | `8px` to `10px` |
| Row/card internal padding | `10px` to `14px` |

Let Fluent components provide their own internal control spacing unless a
component-specific style already exists.

## Component Styling

### Buttons

| Intent | Component style |
| --- | --- |
| Primary submit/create action | `FluentButton Appearance="Appearance.Accent"` |
| Secondary action, refresh, cancel, back | `Appearance.Outline` |
| Toolbar, theme, menu, language, icon-like action | `Appearance.Stealth` |
| Destructive action | Existing button appearance plus `--error` emphasis |

Use one dominant accent action per local surface. Secondary actions should stay
outline or stealth unless they are the next committed step in the workflow.

### Cards And Surfaces

- Use `FluentCard` for content groups, forms, and empty states.
- Do not nest visual cards inside visual cards. Use headings, dividers, or
  neutral role slots inside a card instead.
- Use neutral borders and fills for operational panels. Reserve saturated
  colors for domain status markers and selected states.
- Rows that are clickable should read as rows first and buttons second: full
  width, neutral surface, visible selected state, and a clear domain marker.

### Segmented Controls

Use `ToggleGroup` for mutually exclusive compact choices such as activity,
difficulty, visibility, and attendance. The selected option uses
`--accent-fill-rest` with `--foreground-on-accent-rest`.

Do not use font-weight changes as the selected-state signal; they change text
metrics and make the control feel unstable.

### Forms

- Use Fluent form components where they exist: `FluentTextField`,
  `FluentTextArea`, `FluentSelect`, and `FluentOption`.
- Use the `.field`, `.field__label`, and `.field__input` harmonizer only where
  Fluent UI lacks the needed native input type.
- Group related controls in a `FluentCard` with a compact vertical
  `FluentStack`.
- Keep helper/error text close to the field or action it explains.

### Data And Lists

- Use `FluentDataGrid` for tabular reference data or admin-style records.
- Use list rows for run navigation and roster-like operational data.
- Use role slots for compact counts (`T`, `H`, `D`) and keep the label/value
  relationship visually tight.
- Use skeleton rows only when their shape matches the loaded content shape.

### Feedback

| Situation | Pattern |
| --- | --- |
| Blocking page or card load | `FluentProgressRing` or matching skeleton rows |
| Inline error | `FluentMessageBar Intent="MessageIntent.Error"` |
| Warning / constrained action | `FluentMessageBar Intent="MessageIntent.Warning"` |
| Informational empty state | `FluentMessageBar Intent="MessageIntent.Info"` or `FluentCard` |
| Transient operation result | Toast helper |

## Domain Components

### Difficulty Pill

Difficulty pills are compact, rounded, and non-interactive. Mythic and Mythic+
share the canonical mythic orange but keep distinct labels. Heroic uses the
canonical purple. Normal and LFR stay neutral.

### Attendance Pill

Attendance pills are rounded filled chips with capitalized status text. Use the
status colors from this guide and keep unknown/default status neutral.

### Character Row

Character rows use:

- A class-color side accent.
- Primary character name in stronger weight.
- Class/spec metadata in secondary text.
- Optional circular portrait at `48px`.

### Run List Item

Run list items use:

- A run-kind side accent.
- Title first, then date and signup marker metadata.
- Difficulty pill near the title.
- Role-composition slots beneath the primary metadata.
- A neutral selected background plus a stronger side stripe.

### Guild Crest And Character Portraits

- Guild crests use the Blizzard-provided square image, displayed at `88px`
  with a small radius.
- Character portraits are circular at `48px`.
- Do not recolor or filter Blizzard media assets.

## CSS Ownership

- `app/wwwroot/css/app.css` owns shared visual primitives, app-shell styling,
  and cross-page helper classes.
- Page or component `.razor.css` files are allowed for page-specific or
  reusable-component visual systems. This is the current accepted practice;
  prefer scoped CSS when the styling belongs to one page/component, needs
  local state variants, or should not become a global primitive.
- Inline `Style` is acceptable for small Fluent composition tweaks and
  one-off token usage.
- Move styles from `.razor.css` to `app.css` when the class becomes shared
  vocabulary, app-shell structure, or a cross-page helper.
- Prefer named CSS classes when styling has state variants, domain meaning, or
  repeated visual language.
- Keep class names domain-readable, usually with a BEM-like shape such as
  `.run-list-item__roleslot--short`.
- Keep new visual constants centralized in helpers or CSS classes. Avoid
  unexplained literals in Razor markup.

## Out Of Scope For This File

This guide intentionally does not define:

- Responsive breakpoints, viewport floors, container-query policy, or layout
  correctness checks.
- WCAG checklists, focus behavior, keyboard behavior, landmarks, skip links, or
  forced-colors verification.
- RTL, text expansion, locale rollout, collation, or localization-key policy.
- Core Web Vitals, image loading, font loading, network adaptation, or runtime
  performance targets.
- Component state management, service lifetimes, API contracts, test lanes, or
  repository workflow.

Use the relevant repo guidance or design skill for those topics instead of
adding them here.
