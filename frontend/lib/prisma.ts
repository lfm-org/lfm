import { PrismaClient } from "@prisma/client";
import { PrismaPg } from "@prisma/adapter-pg";

// Lazy singleton — created on first use, never at import time.
// This allows `next build` to succeed without DATABASE_URL.

const globalForPrisma = globalThis as unknown as { prisma?: PrismaClient };

function createClient(): PrismaClient {
  const url = process.env.DATABASE_URL;
  if (!url) throw new Error("DATABASE_URL environment variable is not set");
  return new PrismaClient({ adapter: new PrismaPg({ connectionString: url }) });
}

function getClient(): PrismaClient {
  if (!globalForPrisma.prisma) {
    globalForPrisma.prisma = createClient();
  }
  return globalForPrisma.prisma;
}

// Proxy so callers write `prisma.raid.findMany(...)` as normal,
// but the real client is only created when a method is first accessed.
export const prisma = new Proxy<PrismaClient>({} as PrismaClient, {
  get(_target, prop: string | symbol) {
    return Reflect.get(getClient(), prop);
  },
});
