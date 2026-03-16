import { type ReactNode } from "react";
import { useLocation } from "react-router";
import { getLoginUrl } from "../lib/auth";
import { useAuth } from "../lib/AuthContext";

interface Props {
  children: ReactNode;
}

export default function AuthGuard({ children }: Props) {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) return null;

  if (!user) {
    window.location.href = getLoginUrl(location.pathname);
    return null;
  }

  return <>{children}</>;
}
