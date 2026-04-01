import { Box, Button, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router";
import type { ReactElement } from "react";

interface EmptyStateAction {
  label: string;
  to?: string;
  onClick?: () => void;
}

interface EmptyStateProps {
  icon: ReactElement;
  message: string;
  action?: EmptyStateAction;
}

export default function EmptyState({ icon, message, action }: EmptyStateProps) {
  return (
    <Box sx={{ display: "grid", justifyItems: "center", gap: 2, py: 4 }}>
      <Box sx={{ color: "text.disabled", "& .MuiSvgIcon-root": { fontSize: 48 } }}>{icon}</Box>
      <Typography color="text.secondary">{message}</Typography>
      {action &&
        (action.to ? (
          <Button component={RouterLink} to={action.to} variant="outlined">
            {action.label}
          </Button>
        ) : (
          <Button variant="outlined" onClick={action.onClick}>
            {action.label}
          </Button>
        ))}
    </Box>
  );
}
