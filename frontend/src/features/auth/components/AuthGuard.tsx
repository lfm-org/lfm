import { Box, CircularProgress, Typography } from "@mui/material";
import { Navigate, useLocation } from "react-router";
import { type ReactNode } from "react";
import { useAuth } from "../lib/AuthContext";

interface Props {
  children: ReactNode;
}

export default function AuthGuard({ children }: Props) {
  const { user, loading } = useAuth();
  const location = useLocation();
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
          <Typography color="text.secondary">Checking Battle.net session...</Typography>
        </Box>
      </Box>
    );
  }

  if (!user) {
    return <Navigate to={`/login?redirect=${encodeURIComponent(redirectPath)}`} replace />;
  }

  return <>{children}</>;
}
