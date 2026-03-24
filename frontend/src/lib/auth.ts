import api from "./api";

export interface AuthUser {
  battleNetId: string;
  guildName: string | null;
  selectedCharacterId: string | null;
}

export async function checkAuth(): Promise<AuthUser | null> {
  try {
    const response = await api.get<AuthUser>("/me");
    return response.data;
  } catch {
    return null;
  }
}

export function getLoginUrl(redirect?: string): string {
  const base = `${api.defaults.baseURL}/battlenet/login`;
  return redirect ? `${base}?redirect=${encodeURIComponent(redirect)}` : base;
}

export async function logout(): Promise<void> {
  await api.post("/battlenet/logout");
}
