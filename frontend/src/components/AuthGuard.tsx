import { useEffect, useState, type ReactNode } from "react";
import { useLocation } from "react-router";
import { checkAuth, getLoginUrl, type AuthUser } from "../lib/auth";

interface Props {
  children: ReactNode;
}

export default function AuthGuard({ children }: Props) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const location = useLocation();

  useEffect(() => {
    checkAuth().then(u => {
      if (!u) {
        window.location.href = getLoginUrl(location.pathname);
      } else {
        setUser(u);
      }
    }).finally(() => setLoading(false));
  }, [location.pathname]);

  if (loading) return null;
  return <>{children}</>;
}
