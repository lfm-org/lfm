import { Box, CircularProgress, Typography } from "@mui/material";
import { type ReactNode } from "react";
import { Navigate, useLocation } from "react-router";
import { useTranslation } from "react-i18next";
import { useGuildHome } from "../lib/useGuildHome";

interface Props {
  children: ReactNode;
}

export default function GuildSetupGuard({ children }: Props) {
  const { data, loading, error } = useGuildHome();
  const location = useLocation();
  const { t } = useTranslation();

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
          <CircularProgress aria-label={t("guild.checkingSetup")} />
          <Typography color="text.secondary">{t("guild.checkingSetup")}</Typography>
        </Box>
      </Box>
    );
  }

  if (!error && data?.editor.canEdit && data.setup.requiresSetup && location.pathname !== "/guild") {
    return <Navigate to="/guild?setup=required" replace />;
  }

  return <>{children}</>;
}
