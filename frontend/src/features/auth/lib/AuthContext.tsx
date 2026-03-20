import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { checkAuth, type AuthUser } from "../../../lib/auth";

interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  onCharacterSelected: (selectedCharacterId: string) => void;
}

const AuthContext = createContext<AuthContextValue>({
  user: null,
  loading: true,
  onCharacterSelected: () => {},
});

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  const onCharacterSelected = (selectedCharacterId: string) => {
    setUser(u => u ? { ...u, selectedCharacterId } : u);
  };

  useEffect(() => {
    checkAuth().then(setUser).finally(() => setLoading(false));
  }, []);

  return (
    <AuthContext.Provider value={{ user, loading, onCharacterSelected }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
