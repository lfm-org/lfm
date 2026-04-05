import * as oauth from "oauth4webapi";
import { sealLoginState, verifyLoginState, hashBattleNetId } from "./crypto.js";
import { getRaidersContainer } from "./cosmos.js";
import { toBattleNetIdentity } from "./blizzard-adapters.js";
import {
  getTestModeAccountProfileSummary,
  getTestModeAccessTokenForCallbackCode,
  getTestModeCallbackCodeForScenario,
  getTestModeGuildCrestMedia,
  getTestModeGuildProfile,
  getTestModeGuildRoster,
  getTestModeIdentity,
} from "./test-mode.js";
import type {
  BlizzardAccountProfileSummary,
  BlizzardGuildProfileResponse,
  BlizzardGuildRosterResponse,
  BlizzardMediaSummary,
  BlizzardUserInfo,
} from "../types/blizzard.js";
import type { BattleNetIdentity, RaiderDocument, LoginResponse } from "../types/index.js";

type BattleNetRegion = "eu" | "us" | "kr" | "tw" | "cn";

interface BattleNetTokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  refresh_token?: string;
  scope?: string;
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

// 15 min matches ACCOUNT_CHARS_COOLDOWN_MS in cache.ts, so the identity
// won't expire mid-session while character data is still considered fresh.
// Trade-off: a guild change takes up to 15 min to reflect in the identity.
const IDENTITY_CACHE_TTL_MS = 15 * 60 * 1000;

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
    this.clientId = process.env.LFM_CLIENT_ID || "";
    this.clientSecret = process.env.LFM_CLIENT_SECRET || "";
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

  public async buildAuthorizationUrl(
    redirect?: string,
    testAuthScenario?: string
  ): Promise<{ authUrl: string; loginStateCookie: string | null }> {
    const normalizedRedirect = normalizeRedirectPath(redirect);
    const testModeCallbackCode = getTestModeCallbackCodeForScenario(testAuthScenario);

    if (testModeCallbackCode) {
      // Test mode: skip PKCE, redirect straight to callback
      const url = new URL(this.redirectUri);
      url.searchParams.set("code", testModeCallbackCode);
      url.searchParams.set("state", "test-state");
      url.searchParams.set("redirect", normalizedRedirect);
      return { authUrl: url.toString(), loginStateCookie: null };
    }

    if (!this.clientId || !this.redirectUri) {
      console.warn("Battle.net OAuth is not configured");
      return { authUrl: this.buildFrontendFailureUrl(), loginStateCookie: null };
    }

    const state = oauth.generateRandomState();
    const codeVerifier = oauth.generateRandomCodeVerifier();
    const codeChallenge = await oauth.calculatePKCECodeChallenge(codeVerifier);
    const loginStateCookie = await sealLoginState({ state, codeVerifier, redirect: normalizedRedirect });

    const url = new URL(this.authorizeUrl);
    url.searchParams.set("response_type", "code");
    url.searchParams.set("client_id", this.clientId);
    url.searchParams.set("redirect_uri", this.redirectUri);
    url.searchParams.set("scope", "wow.profile");
    url.searchParams.set("state", state);
    url.searchParams.set("code_challenge", codeChallenge);
    url.searchParams.set("code_challenge_method", "S256");
    return { authUrl: url.toString(), loginStateCookie };
  }

  public async handleCallback(
    code?: string,
    redirect?: string,
    codeVerifier?: string
  ): Promise<LoginResponse | null> {
    if (!code) {
      console.warn("Battle.net callback did not include a code");
      return null;
    }

    const testModeAccessToken = getTestModeAccessTokenForCallbackCode(code);
    let accessToken: string;
    let expiresIn: number;

    if (testModeAccessToken) {
      // Test mode: skip PKCE/state verification
      accessToken = testModeAccessToken;
      expiresIn = 86400;
    } else {
      const token = await this.exchangeCodeForToken(code, codeVerifier);
      if (!token) {
        console.warn("Battle.net handleCallback: token exchange returned null");
        return null;
      }
      accessToken = token.access_token;
      expiresIn = token.expires_in;
    }

    const result = await this.authenticateWithToken(accessToken);
    if (!result) {
      console.warn("Battle.net handleCallback: authenticateWithToken returned null");
      return null;
    }
    return {
      accessToken,
      expiresIn,
      redirect: normalizeRedirectPath(redirect),
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

  public async fetchAccountProfileSummary(
    accessToken: string
  ): Promise<BlizzardAccountProfileSummary> {
    const testSummary = getTestModeAccountProfileSummary(accessToken);
    if (testSummary) return testSummary;

    const url = new URL(`https://${API_HOSTS[this.region]}/profile/user/wow`);
    url.searchParams.set("namespace", this.profileNamespace);
    const response = await fetch(url.toString(), {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    if (!response.ok) {
      throw new Error(`fetchAccountProfileSummary failed: ${response.status}`);
    }
    return response.json() as Promise<BlizzardAccountProfileSummary>;
  }

  public async fetchGuildProfile(
    realmSlug: string,
    guildNameSlug: string,
    accessToken: string
  ): Promise<BlizzardGuildProfileResponse> {
    const testGuildProfile = getTestModeGuildProfile(accessToken, realmSlug, guildNameSlug);
    if (testGuildProfile) return testGuildProfile;

    const url = new URL(
      `https://${API_HOSTS[this.region]}/data/wow/guild/${encodeURIComponent(realmSlug)}/${encodeURIComponent(guildNameSlug)}`
    );
    url.searchParams.set("namespace", this.profileNamespace);
    const response = await fetch(url.toString(), {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    if (!response.ok) {
      const body = await response.text().catch(() => "(unreadable)");
      throw new Error(`fetchGuildProfile failed: ${response.status} ${body}`);
    }
    return response.json() as Promise<BlizzardGuildProfileResponse>;
  }

  public async fetchGuildRoster(
    realmSlug: string,
    guildNameSlug: string,
    accessToken: string
  ): Promise<BlizzardGuildRosterResponse> {
    const testGuildRoster = getTestModeGuildRoster(accessToken, realmSlug, guildNameSlug);
    if (testGuildRoster) return testGuildRoster;

    const url = new URL(
      `https://${API_HOSTS[this.region]}/data/wow/guild/${encodeURIComponent(realmSlug)}/${encodeURIComponent(guildNameSlug)}/roster`
    );
    url.searchParams.set("namespace", this.profileNamespace);
    const response = await fetch(url.toString(), {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    if (!response.ok) {
      const body = await response.text().catch(() => "(unreadable)");
      throw new Error(`fetchGuildRoster failed: ${response.status} ${body}`);
    }
    return response.json() as Promise<BlizzardGuildRosterResponse>;
  }

  public async fetchMediaDocument(
    href: string,
    accessToken: string
  ): Promise<BlizzardMediaSummary> {
    const testMedia = getTestModeGuildCrestMedia(accessToken, href);
    if (testMedia) return testMedia;

    const response = await fetch(href, {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    if (!response.ok) {
      const body = await response.text().catch(() => "(unreadable)");
      throw new Error(`fetchMediaDocument failed: ${response.status} ${body}`);
    }
    return response.json() as Promise<BlizzardMediaSummary>;
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
    const testIdentity = getTestModeIdentity(accessToken);
    if (testIdentity) {
      const container = getRaidersContainer();
      const { resource: existing } = await container.item(testIdentity.battleNetId, testIdentity.battleNetId).read<RaiderDocument>();

      const now = new Date().toISOString();
      let raider: RaiderDocument;
      if (!existing) {
        const newDoc: RaiderDocument = {
          id: testIdentity.battleNetId,
          battleNetId: testIdentity.battleNetId,
          selectedCharacterId: null,
          createdAt: now,
          lastSeenAt: now,
          characters: [],
        };
        const { resource } = await container.items.create<RaiderDocument>(newDoc);
        if (!resource) return null;
        raider = resource;
      } else {
        const { resource } = await container.item(testIdentity.battleNetId, testIdentity.battleNetId).replace<RaiderDocument>({ ...existing, lastSeenAt: now });
        if (!resource) return null;
        raider = resource;
      }

      const selectedCharacter = raider.characters.find(c => c.id === raider.selectedCharacterId) ?? null;
      return {
        identity: toBattleNetIdentity(testIdentity.battleNetId, selectedCharacter, raider.accountGuildsSummary),
        selectedCharacterId: raider.selectedCharacterId,
      };
    }

    const userInfo = await this.fetchUserProfile(accessToken);
    if (!userInfo) {
      console.warn("Battle.net authenticateWithToken: fetchUserProfile returned null");
      return null;
    }
    const battleNetId = hashBattleNetId(userInfo.id);

    const container = getRaidersContainer();
    const { resource: existing } = await container.item(battleNetId, battleNetId).read<RaiderDocument>();

    const now = new Date().toISOString();
    let raider: RaiderDocument;
    if (!existing) {
      const newDoc: RaiderDocument = {
        id: battleNetId,
        battleNetId,
        selectedCharacterId: null,
        createdAt: now,
        lastSeenAt: now,
        characters: [],
      };
      const { resource } = await container.items.create<RaiderDocument>(newDoc);
      if (!resource) return null;
      raider = resource;
    } else {
      const { resource } = await container.item(battleNetId, battleNetId).replace<RaiderDocument>({ ...existing, lastSeenAt: now });
      if (!resource) return null;
      raider = resource;
    }

    const selectedCharacter = raider.characters.find(c => c.id === raider.selectedCharacterId) ?? null;
    return {
      identity: toBattleNetIdentity(battleNetId, selectedCharacter, raider.accountGuildsSummary),
      selectedCharacterId: raider.selectedCharacterId,
    };
  }

  private async exchangeCodeForToken(
    code: string,
    codeVerifier?: string
  ): Promise<BattleNetTokenResponse | null> {
    if (!this.clientSecret) {
      console.warn("Battle.net client secret is not configured");
      return null;
    }
    try {
      const as: oauth.AuthorizationServer = {
        issuer: `https://${AUTH_HOSTS[this.region]}`,
        token_endpoint: this.tokenUrl,
      };
      const client: oauth.Client = { client_id: this.clientId };

      // Reconstruct callback params for oauth4webapi
      const callbackUrl = new URL(this.redirectUri);
      callbackUrl.searchParams.set("code", code);

      const callbackParams = oauth.validateAuthResponse(as, client, callbackUrl);

      const tokenResponse = await oauth.authorizationCodeGrantRequest(
        as,
        client,
        oauth.ClientSecretBasic(this.clientSecret),
        callbackParams,
        this.redirectUri,
        codeVerifier ?? "",
      );

      const tokens = await oauth.processAuthorizationCodeResponse(as, client, tokenResponse);
      return {
        access_token: tokens.access_token,
        token_type: tokens.token_type,
        expires_in: tokens.expires_in ?? 86400,
      };
    } catch (error) {
      console.warn(`Battle.net token exchange error: ${error}`);
      return null;
    }
  }

  private async fetchUserProfile(
    accessToken: string
  ): Promise<BlizzardUserInfo | null> {
    try {
      const response = await fetch(this.userInfoUrl, {
        headers: { Authorization: `Bearer ${accessToken}` },
      });
      if (!response.ok) {
        const text = await response.text().catch(() => "(unreadable)");
        console.warn(`Battle.net fetchUserProfile failed: ${response.status} ${text}`);
        return null;
      }
      return response.json() as Promise<BlizzardUserInfo>;
    } catch (error) {
      console.warn(`Battle.net fetchUserProfile error: ${error}`);
      return null;
    }
  }

}

export const battlenet = new BattlenetService();
