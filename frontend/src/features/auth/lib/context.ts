import { createContext } from "react";
import type { AuthUser } from "../../../lib/auth";

export interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  onCharacterSelected: (selectedCharacterId: string) => void;
  clearAuth: () => void;
  onAccountDeleted: () => void;
  postAuthRedirect: string | null;
  setLocale: (locale: string) => void;
}

export const AuthContext = createContext<AuthContextValue>({
  user: null,
  loading: true,
  onCharacterSelected: () => {},
  clearAuth: () => {},
  onAccountDeleted: () => {},
  postAuthRedirect: null,
  setLocale: () => {},
});
