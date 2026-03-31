import { Box, Button, Stack, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router";
import { useTranslation } from "react-i18next";
import SurfaceCard from "../../../components/SurfaceCard";

export default function LoginFailedPage() {
  const { t } = useTranslation();
  return (
    <Box sx={{ minHeight: "100%", display: "grid", placeItems: "center", px: 2, py: 4 }}>
      <SurfaceCard sx={{ width: "min(100%, 480px)" }}>
        <Stack spacing={2} alignItems="center" textAlign="center">
          <Typography variant="h5" component="h1">
            {t("loginFailed.title")}
          </Typography>
          <Typography color="text.secondary">
            {t("loginFailed.subtitle")}
          </Typography>
          <Button
            component={RouterLink}
            to="/login"
            variant="outlined"
            color="primary"
          >
            {t("loginFailed.button")}
          </Button>
        </Stack>
      </SurfaceCard>
    </Box>
  );
}
