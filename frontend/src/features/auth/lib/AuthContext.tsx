import { useEffect, useState, useCallback, type ReactNode } from "react";
import { useLocation } from "react-router";
import { checkAuth, updateLocale, type AuthUser } from "../../../lib/auth";
import { AuthContext } from "./context";
import i18n, { isSupportedLocale } from "../../../i18n/i18n";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const [postAuthRedirect, setPostAuthRedirect] = useState<string | null>(null);
  const location = useLocation();

  const onCharacterSelected = (selectedCharacterId: string) => {
    setUser(u => u ? { ...u, selectedCharacterId } : u);
  };

  const clearAuth = () => {
    setPostAuthRedirect("/login");
    setUser(null);
  };

  const onAccountDeleted = () => {
    setPostAuthRedirect("/goodbye");
    setUser(null);
  };

  const setLocale = useCallback(async (locale: string) => {
    if (!isSupportedLocale(locale)) return;
    i18n.changeLanguage(locale);
    setUser(u => u ? { ...u, locale } : u);
    if (user) {
      await updateLocale(locale).catch(() => {});
    }
  }, [user]);

  useEffect(() => {
    checkAuth().then((authUser) => {
      setUser(authUser);
      if (authUser?.locale && isSupportedLocale(authUser.locale)) {
        i18n.changeLanguage(authUser.locale);
      }
    }).finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (postAuthRedirect && location.pathname === postAuthRedirect) {
      setPostAuthRedirect(null);
    }
  }, [location.pathname, postAuthRedirect]);

  return (
    <AuthContext.Provider value={{ user, loading, onCharacterSelected, clearAuth, onAccountDeleted, postAuthRedirect, setLocale }}>
      {children}
    </AuthContext.Provider>
  );
}
