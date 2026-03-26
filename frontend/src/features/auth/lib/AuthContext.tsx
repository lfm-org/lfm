import { useEffect, useState, type ReactNode } from "react";
import { useLocation } from "react-router";
import { checkAuth, type AuthUser } from "../../../lib/auth";
import { AuthContext } from "./authContext";

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

  useEffect(() => {
    checkAuth().then(setUser).finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (postAuthRedirect && location.pathname === postAuthRedirect) {
      setPostAuthRedirect(null);
    }
  }, [location.pathname, postAuthRedirect]);

  return (
    <AuthContext.Provider value={{ user, loading, onCharacterSelected, clearAuth, onAccountDeleted, postAuthRedirect }}>
      {children}
    </AuthContext.Provider>
  );
}
