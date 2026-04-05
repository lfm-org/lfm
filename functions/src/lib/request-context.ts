import type { HttpRequest, InvocationContext } from "@azure/functions";
import type { RaiderDocument, GuildDocument } from "../types/index.js";
import { getRaidersContainer, getGuildsContainer } from "./cosmos.js";

export class RequestContext {
  private raiders = new Map<string, RaiderDocument | null>();
  private guilds = new Map<string, GuildDocument | null>();

  async getRaider(battleNetId: string): Promise<RaiderDocument | null> {
    if (this.raiders.has(battleNetId)) return this.raiders.get(battleNetId)!;
    const { resource } = await getRaidersContainer()
      .item(battleNetId, battleNetId)
      .read<RaiderDocument>();
    this.raiders.set(battleNetId, resource ?? null);
    return resource ?? null;
  }

  invalidateRaider(battleNetId: string): void {
    this.raiders.delete(battleNetId);
  }

  async getGuild(guildDocId: string): Promise<GuildDocument | null> {
    if (this.guilds.has(guildDocId)) return this.guilds.get(guildDocId)!;
    const { resource } = await getGuildsContainer()
      .item(guildDocId, guildDocId)
      .read<GuildDocument>();
    this.guilds.set(guildDocId, resource ?? null);
    return resource ?? null;
  }

  invalidateGuild(guildDocId: string): void {
    this.guilds.delete(guildDocId);
  }
}

export function createRequestContext(_req: HttpRequest, _ctx: InvocationContext): RequestContext {
  return new RequestContext();
}
