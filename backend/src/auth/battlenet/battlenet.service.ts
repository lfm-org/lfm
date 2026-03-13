import { Injectable, Logger } from "@nestjs/common";
import { HttpService } from "@nestjs/axios";
import { firstValueFrom } from "rxjs";
import { RaidersService } from "../../raiders/raiders.service";
import { LoginResponseDTO } from "../dto/login-response.dto";
import { BattleNetIdentity } from "./battle-net-identity.interface";

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
  id: string;
  battletag: string;
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

const IDENTITY_CACHE_TTL_MS = 5 * 60 * 1000; // 5 minutes

interface CachedIdentity {
  identity: BattleNetIdentity;
  expiresAt: number;
}

@Injectable()
export class BattlenetService {
  private readonly logger = new Logger(BattlenetService.name);
  private readonly identityCache = new Map<string, CachedIdentity>();
  private readonly clientId: string;
  private readonly clientSecret: string;
  private readonly redirectUri: string;
  private readonly frontendBaseUrl: string;
  private readonly frontendSuccessPath: string;
  private readonly frontendFailurePath: string;
  private readonly region: BattleNetRegion;
  private readonly authorizeUrl: string;
  private readonly tokenUrl: string;
  private readonly userInfoUrl: string;
  private readonly profileNamespace: string;

  constructor(
    private readonly httpService: HttpService,
    private readonly raidersService: RaidersService
  ) {
    this.clientId = process.env.SISU_RAIDCAL_CLIENT_ID || "";
    this.clientSecret = process.env.SISU_RAIDCAL_CLIENT_SECRET || "";
    this.redirectUri =
      process.env.BATTLE_NET_REDIRECT_URI ||
      "http://localhost:3000/battlenet/callback";
    this.frontendBaseUrl =
      process.env.FRONTEND_BASE_URL || "http://localhost:3000";
    this.frontendSuccessPath =
      process.env.BATTLE_NET_LOGIN_SUCCESS_PATH || "/login/success";
    this.frontendFailurePath =
      process.env.BATTLE_NET_LOGIN_FAILURE_PATH || "/login/failed";
    this.region = this.determineRegion(process.env.BATTLE_NET_REGION);
    this.authorizeUrl = `https://${AUTH_HOSTS[this.region]}/oauth/authorize`;
    this.tokenUrl = `https://${AUTH_HOSTS[this.region]}/oauth/token`;
    this.userInfoUrl = `https://${API_HOSTS[this.region]}/oauth/userinfo`;
    this.profileNamespace = PROFILE_NAMESPACES[this.region];
  }

  public async resolveIdentity(
    accessToken: string
  ): Promise<BattleNetIdentity | null> {
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
    if (profile === null) {
      return null;
    }
    const guildName = await this.fetchGuildName(accessToken);
    let raider = await this.raidersService.findOneByBattleNetId(profile.id);
    if (!raider) {
      raider = await this.raidersService.create({
        name: profile.battletag,
        battleNetId: profile.id,
        battleTag: profile.battletag,
        guildName,
      });
    } else {
      raider.name = profile.battletag;
      raider.battleTag = profile.battletag;
      if (guildName) {
        raider.guildName = guildName;
      }
      raider = await this.raidersService.save(raider);
    }
    if (!raider) {
      return null;
    }
    return {
      battleNetId: profile.id,
      battleTag: profile.battletag,
      name: raider.name,
      guildName: raider.guildName,
    };
  }

  private async fetchGuildName(
    accessToken: string
  ): Promise<string | undefined> {
    try {
      const url = this.buildGuildsUrl();
      const response = await firstValueFrom(
        this.httpService.get<any>(url, {
          headers: {
            Authorization: `Bearer ${accessToken}`,
          },
        })
      );
      const data = response.data;
      if (!data) {
        return undefined;
      }
      if (Array.isArray(data) && data.length > 0) {
        return data[0]?.name;
      }
      if (Array.isArray(data?.guilds) && data.guilds.length > 0) {
        return data.guilds[0]?.name;
      }
      if (Array.isArray(data?.guildInfo) && data.guildInfo.length > 0) {
        return data.guildInfo[0]?.name;
      }
      if (typeof data?.guild?.name === "string") {
        return data.guild.name;
      }
      if (typeof data.name === "string") {
        return data.name;
      }
      return undefined;
    } catch (error) {
      this.logger.warn(
        `Battle.net guild lookup failed: ${JSON.stringify(error)}`
      );
      return undefined;
    }
  }

  private buildGuildsUrl(): string {
    const url = new URL(
      `https://${API_HOSTS[this.region]}/profile/user/wow/guilds`
    );
    url.searchParams.set("namespace", this.profileNamespace);
    return url.toString();
  }

  public buildAuthorizationUrl(redirect?: string): string {
    if (!this.clientId || !this.redirectUri) {
      this.logger.warn("Battle.net OAuth is not configured");
      return this.buildFrontendFailureUrl();
    }
    const normalizedRedirect = this.normalizeRedirectPath(redirect);
    const payload = this.encodeState({ redirect: normalizedRedirect });
    const url = new URL(this.authorizeUrl);
    url.searchParams.set("response_type", "code");
    url.searchParams.set("client_id", this.clientId);
    url.searchParams.set("redirect_uri", this.redirectUri);
    url.searchParams.set("scope", "openid wow.profile");
    url.searchParams.set("state", payload);
    return url.toString();
  }

  public async handleCallback(
    code?: string,
    state?: string
  ): Promise<LoginResponseDTO | null> {
    if (!code) {
      this.logger.warn("Battle.net callback did not include a code");
      return null;
    }
    const parsedState = this.decodeState(state);
    const normalizedRedirect = this.normalizeRedirectPath(parsedState?.redirect);
    const token = await this.exchangeCodeForToken(code);
    if (token === null) {
      return null;
    }
    const identity = await this.authenticateWithToken(token.access_token);
    if (!identity) {
      return null;
    }
    return {
      accessToken: token.access_token,
      refreshToken: token.refresh_token,
      name: identity.name,
      redirect: normalizedRedirect,
      battleNetId: identity.battleNetId,
      guildName: identity.guildName,
    };
  }

  public buildFrontendSuccessUrl(payload: LoginResponseDTO): string {
    const base = this.buildFrontendBase(this.frontendSuccessPath);
    const url = new URL(base);
    url.searchParams.set("access_token", payload.accessToken);
    if (payload.name) {
      url.searchParams.set("name", payload.name);
    }
    if (payload.redirect) {
      url.searchParams.set("redirect", payload.redirect);
    }
    if (payload.guildName) {
      url.searchParams.set("guild", payload.guildName);
    }
    return url.toString();
  }

  public buildFrontendFailureUrl(): string {
    return this.buildFrontendBase(this.frontendFailurePath);
  }

  private async exchangeCodeForToken(
    code: string
  ): Promise<BattleNetTokenResponse | null> {
    if (!this.clientSecret) {
      this.logger.warn("Battle.net client secret is not configured");
      return null;
    }
    try {
      const body = new URLSearchParams();
      body.set("grant_type", "authorization_code");
      body.set("code", code);
      body.set("redirect_uri", this.redirectUri);
      const response = await firstValueFrom(
        this.httpService.post<BattleNetTokenResponse>(
          this.tokenUrl,
          body.toString(),
          {
            headers: {
              "Content-Type": "application/x-www-form-urlencoded",
            },
            auth: {
              username: this.clientId,
              password: this.clientSecret,
            },
          }
        )
      );
      return response.data;
    } catch (error) {
      this.logger.warn(
        `Battle.net token exchange failed: ${JSON.stringify(error)}`
      );
      return null;
    }
  }

  private async fetchUserProfile(
    accessToken: string
  ): Promise<BattleNetUserInfo | null> {
    try {
      const response = await firstValueFrom(
        this.httpService.get<BattleNetUserInfo>(this.userInfoUrl, {
          headers: {
            Authorization: `Bearer ${accessToken}`,
          },
        })
      );
      return response.data;
    } catch (error) {
      this.logger.warn(
        `Battle.net profile lookup failed: ${JSON.stringify(error)}`
      );
      return null;
    }
  }

  private decodeState(state?: string): BattleNetLoginState | null {
    if (!state) {
      return null;
    }
    try {
      const raw = decodeURIComponent(state);
      const decoded = Buffer.from(raw, "base64").toString("utf-8");
      return JSON.parse(decoded) as BattleNetLoginState;
    } catch (error) {
      this.logger.warn(`Battle.net state decode failed: ${error}`);
      return null;
    }
  }

  private encodeState(state: BattleNetLoginState): string {
    const raw = JSON.stringify(state);
    return Buffer.from(raw, "utf-8").toString("base64");
  }

  private normalizeRedirectPath(path?: string): string {
    if (!path) {
      return "/";
    }
    const trimmed = path.trim();
    if (!trimmed.startsWith("/")) {
      return "/";
    }
    return trimmed;
  }

  private buildFrontendBase(path: string): string {
    const normalizedBase = this.frontendBaseUrl.endsWith("/")
      ? this.frontendBaseUrl.slice(0, -1)
      : this.frontendBaseUrl;
    return `${normalizedBase}${path}`;
  }

  private determineRegion(value?: string): BattleNetRegion {
    const normalized = (value || "eu").toLowerCase();
    if (["us", "kr", "tw", "cn"].includes(normalized)) {
      return normalized as BattleNetRegion;
    }
    return "eu";
  }
}
