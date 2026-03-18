import { signState, verifyState, hashBattleNetId } from "./crypto.js";
import { getRaidersContainer } from "./cosmos.js";
import { getTestModeAccountCharacters, getTestModeIdentity } from "./test-mode.js";
import type { BattleNetIdentity, RaiderDocument, LoginResponse, AccountCharacter } from "../types/index.js";

type BattleNetRegion = "eu" | "us" | "kr" | "tw" | "cn";

interface BattleNetLoginState {
  redirect?: string;
}

interface BattleNetTokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  refresh_token?: string;
  scope?: string;
}

interface BattleNetUserInfo {
  id: number;
  battletag: string;
}

interface AuthenticationResult {
  identity: BattleNetIdentity;
  selectedCharacterId: string | null;
}

const AUTH_HOSTS: Record<BattleNetRegion, string> = {
  eu: "eu.battle.net",
  us: "us.battle.net",
  kr: "kr.battle.net",
  tw: "tw.battle.net",
  cn: "gateway.battlenet.com.cn",
};

const API_HOSTS: Record<BattleNetRegion, string> = {
  eu: "eu.api.blizzard.com",
  us: "us.api.blizzard.com",
  kr: "kr.api.blizzard.com",
  tw: "tw.api.blizzard.com",
  cn: "gateway.battlenet.com.cn",
};

const PROFILE_NAMESPACES: Record<BattleNetRegion, string> = {
  eu: "profile-eu",
  us: "profile-us",
  kr: "profile-kr",
  tw: "profile-tw",
  cn: "profile-cn",
};

const IDENTITY_CACHE_TTL_MS = 5 * 60 * 1000;

interface CachedIdentity {
  identity: BattleNetIdentity;
  expiresAt: number;
}

function determineRegion(value?: string): BattleNetRegion {
  const normalized = (value || "eu").toLowerCase();
  if (["us", "kr", "tw", "cn"].includes(normalized)) {
    return normalized as BattleNetRegion;
  }
  return "eu";
}

export function normalizeRedirectPath(path?: string): string {
  if (!path) return "/";
  const trimmed = path.trim();
  if (!trimmed.startsWith("/")) return "/";
  return trimmed;
}

export class BattlenetService {
  private readonly identityCache = new Map<string, CachedIdentity>();
  private readonly clientId: string;
  private readonly clientSecret: string;
  private readonly redirectUri: string;
  private readonly appBaseUrl: string;
  private readonly region: BattleNetRegion;
  private readonly authorizeUrl: string;
  private readonly tokenUrl: string;
  private readonly userInfoUrl: string;
  private readonly profileNamespace: string;

  constructor() {
    this.clientId = process.env.SISU_RAIDCAL_CLIENT_ID || "";
    this.clientSecret = process.env.SISU_RAIDCAL_CLIENT_SECRET || "";
    this.redirectUri =
      process.env.BATTLE_NET_REDIRECT_URI ||
      "http://localhost:7071/api/battlenet/callback";
    this.appBaseUrl = process.env.APP_BASE_URL || "http://localhost:5173";
    this.region = determineRegion(process.env.BATTLE_NET_REGION);
    this.authorizeUrl = `https://${AUTH_HOSTS[this.region]}/oauth/authorize`;
    this.tokenUrl = `https://${AUTH_HOSTS[this.region]}/oauth/token`;
    this.userInfoUrl = `https://${AUTH_HOSTS[this.region]}/oauth/userinfo`;
    this.profileNamespace = PROFILE_NAMESPACES[this.region];
  }

  public buildAuthorizationUrl(redirect?: string): string {
    if (!this.clientId || !this.redirectUri) {
      console.warn("Battle.net OAuth is not configured");
      return this.buildFrontendFailureUrl();
    }
    const normalizedRedirect = normalizeRedirectPath(redirect);
    const state = this.encodeState({ redirect: normalizedRedirect });
    const url = new URL(this.authorizeUrl);
    url.searchParams.set("response_type", "code");
    url.searchParams.set("client_id", this.clientId);
    url.searchParams.set("redirect_uri", this.redirectUri);
    url.searchParams.set("scope", "openid wow.profile");
    url.searchParams.set("state", state);
    return url.toString();
  }

  public async handleCallback(
    code?: string,
    state?: string
  ): Promise<LoginResponse | null> {
    if (!code) {
      console.warn("Battle.net callback did not include a code");
      return null;
    }
    const parsedState = this.decodeState(state);
    const redirect = normalizeRedirectPath(parsedState?.redirect);
    const token = await this.exchangeCodeForToken(code);
    if (!token) {
      console.warn("Battle.net handleCallback: token exchange returned null");
      return null;
    }
    const result = await this.authenticateWithToken(token.access_token);
    if (!result) {
      console.warn("Battle.net handleCallback: authenticateWithToken returned null");
      return null;
    }
    return {
      accessToken: token.access_token,
      expiresIn: token.expires_in,
      redirect,
      guildName: result.identity.guildName,
      selectedCharacterId: result.selectedCharacterId,
    };
  }

  public buildFrontendSuccessUrl(payload: LoginResponse): string {
    const url = new URL(`${this.appBaseUrl}/login/success`);
    if (payload.redirect) url.searchParams.set("redirect", payload.redirect);
    return url.toString();
  }

  public buildFrontendFailureUrl(): string {
    return `${this.appBaseUrl}/login/failed`;
  }

  public async fetchAccountCharacters(
    accessToken: string
  ): Promise<AccountCharacter[]> {
    const testCharacters = getTestModeAccountCharacters(accessToken, this.region);
    if (testCharacters) return testCharacters;

    const url = new URL(`https://${API_HOSTS[this.region]}/profile/user/wow`);
    url.searchParams.set("namespace", this.profileNamespace);
    const response = await fetch(url.toString(), {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    if (!response.ok) {
      throw new Error(`fetchAccountCharacters failed: ${response.status}`);
    }
    const data = await response.json() as {
      wow_accounts?: Array<{
        characters?: Array<{
          name: string;
          realm: { slug: string; name: string | Record<string, string> };
          level: number;
        }>;
      }>;
    };
    return (data.wow_accounts ?? []).flatMap((account) =>
      (account.characters ?? []).map((c) => ({
        name: c.name,
        realm: c.realm.slug,
        realmName: typeof c.realm.name === "string"
          ? c.realm.name
          : (c.realm.name as Record<string, string>).en_US
            ?? (c.realm.name as Record<string, string>).en_GB
            ?? c.realm.slug,
        level: c.level,
        region: this.region,
      }))
    );
  }

  public async resolveIdentity(
    accessToken: string
  ): Promise<BattleNetIdentity | null> {
    const testIdentity = getTestModeIdentity(accessToken);
    if (testIdentity) return testIdentity;

    const cached = this.identityCache.get(accessToken);
    if (cached && cached.expiresAt > Date.now()) {
      return cached.identity;
    }
    const result = await this.authenticateWithToken(accessToken);
    if (result) {
      this.identityCache.set(accessToken, {
        identity: result.identity,
        expiresAt: Date.now() + IDENTITY_CACHE_TTL_MS,
      });
    }
    return result?.identity ?? null;
  }

  private async authenticateWithToken(
    accessToken: string
  ): Promise<AuthenticationResult | null> {
    const profile = await this.fetchUserProfile(accessToken);
    if (!profile) {
      console.warn("Battle.net authenticateWithToken: fetchUserProfile returned null");
      return null;
    }
    const guild = await this.fetchGuild(accessToken);
    const battleNetId = hashBattleNetId(profile.id);

    const container = getRaidersContainer();
    const { resource: existing } = await container.item(battleNetId, battleNetId).read<RaiderDocument>();

    let raider: RaiderDocument;
    if (!existing) {
      const now = new Date().toISOString();
      const newDoc: RaiderDocument = {
        id: battleNetId,
        battleNetId,
        guildName: guild?.name ?? null,
        guildId: guild?.id ?? null,
        selectedCharacterId: null,
        createdAt: now,
        characters: [],
      };
      const { resource } = await container.items.create<RaiderDocument>(newDoc);
      if (!resource) return null;
      raider = resource;
    } else {
      const updated: RaiderDocument = {
        ...existing,
        ...(guild ? { guildName: guild.name, guildId: guild.id } : {}),
      };
      const { resource } = await container.item(battleNetId, battleNetId).replace<RaiderDocument>(updated);
      if (!resource) return null;
      raider = resource;
    }

    return {
      identity: { battleNetId, guildName: raider.guildName, guildId: raider.guildId ?? null },
      selectedCharacterId: raider.selectedCharacterId,
    };
  }

  private async exchangeCodeForToken(
    code: string
  ): Promise<BattleNetTokenResponse | null> {
    if (!this.clientSecret) {
      console.warn("Battle.net client secret is not configured");
      return null;
    }
    try {
      const body = new URLSearchParams();
      body.set("grant_type", "authorization_code");
      body.set("code", code);
      body.set("redirect_uri", this.redirectUri);

      const credentials = Buffer.from(
        `${this.clientId}:${this.clientSecret}`
      ).toString("base64");

      const response = await fetch(this.tokenUrl, {
        method: "POST",
        headers: {
          "Content-Type": "application/x-www-form-urlencoded",
          Authorization: `Basic ${credentials}`,
        },
        body: body.toString(),
      });

      if (!response.ok) {
        const text = await response.text().catch(() => "(unreadable)");
        console.warn(`Battle.net token exchange failed: ${response.status} ${text}`);
        return null;
      }
      return response.json() as Promise<BattleNetTokenResponse>;
    } catch (error) {
      console.warn(`Battle.net token exchange error: ${error}`);
      return null;
    }
  }

  private async fetchUserProfile(
    accessToken: string
  ): Promise<BattleNetUserInfo | null> {
    try {
      const response = await fetch(this.userInfoUrl, {
        headers: { Authorization: `Bearer ${accessToken}` },
      });
      if (!response.ok) {
        const text = await response.text().catch(() => "(unreadable)");
        console.warn(`Battle.net fetchUserProfile failed: ${response.status} ${text}`);
        return null;
      }
      return response.json() as Promise<BattleNetUserInfo>;
    } catch (error) {
      console.warn(`Battle.net fetchUserProfile error: ${error}`);
      return null;
    }
  }

  private async fetchGuild(
    accessToken: string
  ): Promise<{ name: string; id: number } | undefined> {
    try {
      const url = new URL(
        `https://${API_HOSTS[this.region]}/profile/user/wow/guilds`
      );
      url.searchParams.set("namespace", this.profileNamespace);
      const response = await fetch(url.toString(), {
        headers: { Authorization: `Bearer ${accessToken}` },
      });
      if (!response.ok) return undefined;
      const data = await response.json() as {
        guilds?: Array<{ guild?: { id?: number; name?: string } }>;
      };
      if (!Array.isArray(data?.guilds) || data.guilds.length === 0) return undefined;
      const g = data.guilds[0]?.guild;
      if (!g?.id || !g?.name) return undefined;
      return { id: g.id, name: g.name };
    } catch {
      return undefined;
    }
  }

  private encodeState(state: BattleNetLoginState): string {
    const json = Buffer.from(JSON.stringify(state), "utf-8").toString("base64");
    return signState(json);
  }

  private decodeState(state?: string): BattleNetLoginState | null {
    if (!state) return null;
    const raw = decodeURIComponent(state);
    const verified = verifyState(raw);
    if (!verified) return null;
    try {
      return JSON.parse(Buffer.from(verified, "base64").toString("utf-8"));
    } catch {
      return null;
    }
  }
}

export const battlenet = new BattlenetService();
