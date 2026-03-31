import { Box, CircularProgress, Typography } from "@mui/material";
import { Navigate, useLocation } from "react-router";
import { type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "../lib/useAuth";

interface Props {
  children: ReactNode;
}

export default function AuthGuard({ children }: Props) {
  const { user, loading, postAuthRedirect } = useAuth();
  const location = useLocation();
  const { t } = useTranslation();
  const redirectPath = `${location.pathname}${location.search}`;

  if (loading) {
    return (
      <Box
        sx={{
          minHeight: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          p: 4,
        }}
      >
        <Box sx={{ display: "grid", justifyItems: "center", gap: 2 }}>
          <CircularProgress />
          <Typography color="text.secondary">{t("auth.checkingSession")}</Typography>
        </Box>
      </Box>
    );
  }

  if (!user) {
    if (postAuthRedirect) {
      return <Navigate to={postAuthRedirect} replace />;
    }
    return <Navigate to={`/login?redirect=${encodeURIComponent(redirectPath)}`} replace />;
  }

  return <>{children}</>;
}
