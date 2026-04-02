import { Alert, Box, Button } from "@mui/material";
import { useTranslation } from "react-i18next";

interface ErrorStateProps {
  message: string;
  onRetry?: () => void;
}

export default function ErrorState({ message, onRetry }: ErrorStateProps) {
  const { t } = useTranslation();

  return (
    <Box sx={{ display: "grid", gap: 1.5 }}>
      <Alert severity="error">{message}</Alert>
      {onRetry && (
        <Box>
          <Button variant="outlined" size="small" onClick={onRetry}>
            {t("common.tryAgain")}
          </Button>
        </Box>
      )}
    </Box>
  );
}
