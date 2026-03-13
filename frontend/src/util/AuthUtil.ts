const ACCESS_TOKEN_KEY = "raidcal_access_token";
const NAME_KEY = "raidcal_name";
const GUILD_KEY = "raidcal_guild";

export function setAccessToken(token: string) {
  localStorage.setItem(ACCESS_TOKEN_KEY, token);
}

export function getAccessToken(): string | undefined {
  return localStorage.getItem(ACCESS_TOKEN_KEY) || undefined;
}

export function clearAccessToken() {
  localStorage.removeItem(ACCESS_TOKEN_KEY);
}

export function setDisplayName(name: string) {
  localStorage.setItem(NAME_KEY, name);
}

export function getDisplayName(): string | undefined {
  return localStorage.getItem(NAME_KEY) || undefined;
}

export function setGuildName(guild: string) {
  localStorage.setItem(GUILD_KEY, guild);
}

export function getGuildName(): string | undefined {
  return localStorage.getItem(GUILD_KEY) || undefined;
}
