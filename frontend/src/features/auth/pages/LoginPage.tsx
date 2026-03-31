import { Box, Button, Stack, Typography } from "@mui/material";
import { useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { getLoginUrl } from "../../../lib/auth";
import SurfaceCard from "../../../components/SurfaceCard";

export default function LoginPage() {
  const [searchParams] = useSearchParams();
  const { t } = useTranslation();
  const redirectPath = searchParams.get("redirect") || "/raids";

  return (
    <Box sx={{ minHeight: "100%", display: "grid", placeItems: "center", px: 2, py: 4 }}>
      <SurfaceCard sx={{ width: "min(100%, 480px)" }}>
        <Stack spacing={2} alignItems="center" textAlign="center">
          <Typography variant="h4" component="h1">
            {t("login.title")}
          </Typography>
          <Typography color="text.secondary">
            {t("login.subtitle")}
          </Typography>
          <Button
            variant="contained"
            color="primary"
            size="large"
            disableElevation
            href={getLoginUrl(redirectPath)}
          >
            {t("login.button")}
          </Button>
        </Stack>
      </SurfaceCard>
    </Box>
  );
}
