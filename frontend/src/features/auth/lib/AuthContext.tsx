import { useEffect, useState, useCallback, type ReactNode } from "react";
import { useLocation } from "react-router";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { checkAuth, updateLocale, type AuthUser } from "../../../lib/auth";
import { AuthContext } from "./context";
import { queryKeys } from "../../../lib/queryKeys";
import i18n, { isSupportedLocale } from "../../../i18n/i18n";

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [postAuthRedirect, setPostAuthRedirect] = useState<string | null>(null);
  const location = useLocation();

  const { data: user = null, isPending } = useQuery({
    queryKey: queryKeys.me(),
    queryFn: checkAuth,
    staleTime: 10 * 60_000,
  });

  // Set i18n language when user data arrives
  useEffect(() => {
    if (user?.locale && isSupportedLocale(user.locale)) {
      i18n.changeLanguage(user.locale);
    }
  }, [user?.locale]);

  const onCharacterSelected = (selectedCharacterId: string) => {
    queryClient.setQueryData<AuthUser | null>(queryKeys.me(), (prev) =>
      prev ? { ...prev, selectedCharacterId } : (prev ?? null)
    );
  };

  const clearAuth = () => {
    setPostAuthRedirect("/login");
    queryClient.setQueryData(queryKeys.me(), null);
  };

  const onAccountDeleted = () => {
    setPostAuthRedirect("/goodbye");
    queryClient.setQueryData(queryKeys.me(), null);
  };

  const setLocale = useCallback(async (locale: string) => {
    if (!isSupportedLocale(locale)) return;
    i18n.changeLanguage(locale);
    queryClient.setQueryData<AuthUser | null>(queryKeys.me(), (prev) =>
      prev ? { ...prev, locale } : (prev ?? null)
    );
    if (user) {
      await updateLocale(locale).catch(() => {});
    }
  }, [user, queryClient]);

  useEffect(() => {
    if (postAuthRedirect && location.pathname === postAuthRedirect) {
      setPostAuthRedirect(null);
    }
  }, [location.pathname, postAuthRedirect]);

  return (
    <AuthContext.Provider value={{ user, loading: isPending, onCharacterSelected, clearAuth, onAccountDeleted, postAuthRedirect, setLocale }}>
      {children}
    </AuthContext.Provider>
  );
}
