import { Button, Stack, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router";
import { useTranslation } from "react-i18next";
import PageContainer from "../../../components/layout/PageContainer";
import SurfaceCard from "../../../components/SurfaceCard";
import useDocumentTitle from "../../../hooks/useDocumentTitle";

export default function GoodbyePage() {
  const { t } = useTranslation();
  useDocumentTitle(`${t("goodbye.title")} — LFM`);
  return (
    <PageContainer>
      <SurfaceCard sx={{ maxWidth: 560, mx: "auto" }}>
        <Stack spacing={2} textAlign="center" alignItems="center">
          <Typography variant="h4" component="h1">
            {t("goodbye.title")}
          </Typography>
          <Typography color="text.secondary">
            {t("goodbye.body1")}
          </Typography>
          <Typography color="text.secondary">
            {t("goodbye.body2")}
          </Typography>
          <Button component={RouterLink} to="/login" variant="contained">
            {t("goodbye.button")}
          </Button>
        </Stack>
      </SurfaceCard>
    </PageContainer>
  );
}
