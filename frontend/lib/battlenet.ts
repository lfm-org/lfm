import { prisma } from "./prisma";

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

export interface BattleNetIdentity {
  battleNetId: string;
  battleTag: string;
  name?: string;
  guildName?: string | null;
}

export interface LoginResponse {
  accessToken: string;
  name?: string;
  redirect?: string;
  guildName?: string | null;
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

function normalizeRedirectPath(path?: string): string {
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
      "http://localhost:3001/api/battlenet/callback";
    this.appBaseUrl = process.env.APP_BASE_URL || "http://localhost:3001";
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
    const identity = await this.authenticateWithToken(token.access_token);
    if (!identity) {
      console.warn("Battle.net handleCallback: authenticateWithToken returned null");
      return null;
    }
    return {
      accessToken: token.access_token,
      name: identity.name,
      redirect,
      guildName: identity.guildName,
    };
  }

  public buildFrontendSuccessUrl(payload: LoginResponse): string {
    const base = this.buildBase("/login/success");
    const url = new URL(base);
    if (payload.name) url.searchParams.set("name", payload.name);
    if (payload.redirect) url.searchParams.set("redirect", payload.redirect);
    if (payload.guildName) url.searchParams.set("guild", payload.guildName);
    return url.toString();
  }

  public buildFrontendFailureUrl(): string {
    return this.buildBase("/login/failed");
  }

  public async resolveIdentity(
    accessToken: string
  ): Promise<BattleNetIdentity | null> {
    if (process.env.TEST_MODE === "true" && accessToken === "test_battlenet_token") {
      return {
        battleNetId: "test-bnet-id",
        battleTag: "TestUser#1234",
        name: "TestUser#1234",
        guildName: null,
      };
    }
    const cached = this.identityCache.get(accessToken);
    if (cached && cached.expiresAt > Date.now()) {
      return cached.identity;
    }
    const identity = await this.authenticateWithToken(accessToken);
    if (identity) {
      this.identityCache.set(accessToken, {
        identity,
        expiresAt: Date.now() + IDENTITY_CACHE_TTL_MS,
      });
    }
    return identity;
  }

  private async authenticateWithToken(
    accessToken: string
  ): Promise<BattleNetIdentity | null> {
    const profile = await this.fetchUserProfile(accessToken);
    if (!profile) {
      console.warn("Battle.net authenticateWithToken: fetchUserProfile returned null");
      return null;
    }
    const guildName = await this.fetchGuildName(accessToken);

    const battleNetId = String(profile.id);

    let raider = await prisma.raider.findUnique({
      where: { battleNetId },
    });

    if (!raider) {
      raider = await prisma.raider.create({
        data: {
          name: profile.battletag,
          battleNetId,
          battleTag: profile.battletag,
          guildName,
        },
      });
    } else {
      raider = await prisma.raider.update({
        where: { id: raider.id },
        data: {
          name: profile.battletag,
          battleTag: profile.battletag,
          ...(guildName ? { guildName } : {}),
        },
      });
    }

    if (!raider) return null;

    return {
      battleNetId,
      battleTag: profile.battletag,
      name: raider.name ?? undefined,
      guildName: raider.guildName,
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
        const body = await response.text().catch(() => "(unreadable)");
        console.warn(`Battle.net token exchange failed: ${response.status} ${body}`);
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
        const body = await response.text().catch(() => "(unreadable)");
        console.warn(`Battle.net fetchUserProfile failed: ${response.status} ${body}`);
        return null;
      }
      return response.json() as Promise<BattleNetUserInfo>;
    } catch (error) {
      console.warn(`Battle.net fetchUserProfile error: ${error}`);
      return null;
    }
  }

  private async fetchGuildName(
    accessToken: string
  ): Promise<string | undefined> {
    try {
      const url = new URL(
        `https://${API_HOSTS[this.region]}/profile/user/wow/guilds`
      );
      url.searchParams.set("namespace", this.profileNamespace);
      const response = await fetch(url.toString(), {
        headers: { Authorization: `Bearer ${accessToken}` },
      });
      if (!response.ok) return undefined;
      const data = await response.json();
      if (!data) return undefined;
      if (Array.isArray(data) && data.length > 0) return data[0]?.name;
      if (Array.isArray(data?.guilds) && data.guilds.length > 0)
        return data.guilds[0]?.name;
      if (Array.isArray(data?.guildInfo) && data.guildInfo.length > 0)
        return data.guildInfo[0]?.name;
      if (typeof data?.guild?.name === "string") return data.guild.name;
      if (typeof data.name === "string") return data.name;
      return undefined;
    } catch {
      return undefined;
    }
  }

  private encodeState(state: BattleNetLoginState): string {
    return Buffer.from(JSON.stringify(state), "utf-8").toString("base64");
  }

  private decodeState(state?: string): BattleNetLoginState | null {
    if (!state) return null;
    try {
      const raw = decodeURIComponent(state);
      const decoded = Buffer.from(raw, "base64").toString("utf-8");
      return JSON.parse(decoded) as BattleNetLoginState;
    } catch {
      return null;
    }
  }

  private buildBase(path: string): string {
    const base = this.appBaseUrl.endsWith("/")
      ? this.appBaseUrl.slice(0, -1)
      : this.appBaseUrl;
    return `${base}${path}`;
  }
}

export const battlenet = new BattlenetService();
