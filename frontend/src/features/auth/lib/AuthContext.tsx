import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { useLocation } from "react-router";
import { checkAuth, type AuthUser } from "../../../lib/auth";

interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  onCharacterSelected: (selectedCharacterId: string) => void;
  clearAuth: () => void;
  onAccountDeleted: () => void;
  postAuthRedirect: string | null;
}

const AuthContext = createContext<AuthContextValue>({
  user: null,
  loading: true,
  onCharacterSelected: () => {},
  clearAuth: () => {},
  onAccountDeleted: () => {},
  postAuthRedirect: null,
});

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

export function useAuth() {
  return useContext(AuthContext);
}
