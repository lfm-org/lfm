import api from "./api";

export interface AuthUser {
  battleNetId: string;
  guildName: string | null;
  selectedCharacterId: string | null;
  isSiteAdmin: boolean;
  locale: string | null;
}

export async function checkAuth(): Promise<AuthUser | null> {
  try {
    const response = await api.get<AuthUser>("/me");
    return response.data;
  } catch {
    return null;
  }
}

export async function updateLocale(locale: string): Promise<void> {
  await api.patch("/me", { locale });
}

export function getLoginUrl(redirect?: string): string {
  const base = `${api.defaults.baseURL}/battlenet/login`;
  return redirect ? `${base}?redirect=${encodeURIComponent(redirect)}` : base;
}

export async function logout(): Promise<void> {
  await api.post("/battlenet/logout");
}

export async function deleteAccount(): Promise<void> {
  await api.delete("/me");
}
