import { Box, CircularProgress, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";

interface LoadingStateProps {
  label?: string;
}

export default function LoadingState({ label }: LoadingStateProps) {
  const { t } = useTranslation();
  const text = label ?? t("common.loading");

  return (
    <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", gap: 1.5, py: 6 }}>
      <CircularProgress size={24} aria-label={text} />
      <Typography color="text.secondary">{text}</Typography>
    </Box>
  );
}
