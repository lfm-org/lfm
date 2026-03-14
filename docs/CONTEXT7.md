# Context7 Library Documentation

Before making non-trivial changes to code that uses an external library, use the Context7 MCP tools to pull up-to-date documentation rather than relying on training-data knowledge.

## Workflow

1. Call `mcp__context7__resolve-library-id` with the library name and a short description of what you need.
2. Use the returned library ID to call `mcp__context7__query-docs` with a specific question.

## Key Library IDs for This Project

| Library | Context7 ID |
|---------|-------------|
| Next.js | `/vercel/next.js` |
| Prisma | `/prisma/prisma` |
| MUI v6 | `/mui/material-ui` |
| TanStack Table v8 | `/tanstack/table` |
| Compose Specification | `/compose-spec/compose-spec` |

## When to Use It

- Next.js App Router, Route Handlers, middleware, Server Components
- Prisma query builder, relations, migrations, upsert patterns
- MUI v6 components, ThemeRegistry, emotion cache
- TanStack Table v8 API (`useReactTable`, `createColumnHelper`, `flexRender`)
- Any time a version upgrade is involved — check the changelog via context7 first
