# Data-Driven Development

**Principle:** Configuration belongs in one place, not scattered as repeated literals.

## Named Constants for Magic Strings

Repeated string literals are a maintenance hazard and a source of hard-to-trace bugs.

- **Cookie name** — `battlenet_token` appears in multiple files. Define it once in a constants module and import it everywhere.
- **Route paths** — `/login/success`, `/login/failed`, `/raids` should be defined in a shared constants file, not inlined at each redirect.
- **Cache TTLs, delays, thresholds** — name them at the top of the module (`const CACHE_TTL_MS = 5 * 60 * 1000`) rather than embedding arithmetic inline.

## Config Arrays Over Repeated Procedural Logic

When the same operation must be performed for N entities of the same shape, prefer an array of definitions and a single loop over N copies of control flow.

**Canonical example — `app/api/wow/update/route.ts`:**

```typescript
interface EntitySyncDef {
  name: string;
  fetch: () => Promise<unknown[]>;
  upsert: (item: unknown) => Promise<void>;
}

const ENTITY_SYNC_DEFS: EntitySyncDef[] = [
  { name: "realms",  fetch: fetchRealms,  upsert: upsertRealm  },
  { name: "classes", fetch: fetchClasses, upsert: upsertClass  },
];

for (const def of ENTITY_SYNC_DEFS) {
  const items = await def.fetch();
  for (const item of items) await def.upsert(item);
}
```

Adding a new Blizzard entity type = one new entry in the array, no new control flow.

## Environment Variables for Environment-Specific Values

- Callback URIs, app base URLs — always from `process.env`, never default to localhost strings in production-path code.
- Region, namespace, grant type — env var with a documented default in `example.env`.

## What Is NOT Data-Driven (Acceptable Hard-Coding)

- Enum values from the Prisma schema (e.g. `RaidVisibility.PUBLIC`) — single source of truth is the schema.
- Framework-required literals (e.g. Next.js `matcher` in `middleware.ts`) — where the framework demands a literal.
- Test mock URLs like `http://localhost/api/raids` — test-only, not production config.
