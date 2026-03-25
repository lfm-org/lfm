import { Box, CircularProgress, Typography } from "@mui/material";
import { type ReactNode } from "react";
import { Navigate, useLocation } from "react-router";
import { useGuildHome } from "../lib/useGuildHome";

interface Props {
  children: ReactNode;
}

export default function GuildSetupGuard({ children }: Props) {
  const { data, loading, error } = useGuildHome();
  const location = useLocation();

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
          <Typography color="text.secondary">Checking guild setup...</Typography>
        </Box>
      </Box>
    );
  }

  if (!error && data?.editor.canEdit && data.setup.requiresSetup && location.pathname !== "/guild") {
    return <Navigate to="/guild" replace />;
  }

  return <>{children}</>;
}
