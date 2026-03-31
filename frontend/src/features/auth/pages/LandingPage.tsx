import { Box, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import PageContainer from "../../../components/layout/PageContainer";
import SurfaceCard from "../../../components/SurfaceCard";
import { surface } from "../../../theme";

const valuePropKeys = [
  "sharedSchedule",
  "roleCoverage",
  "battleNetSignIn",
] as const;

export default function LandingPage() {
  const { t } = useTranslation();
  return (
    <PageContainer>
      <SurfaceCard
        sx={{
          overflow: "hidden",
          borderRadius: 4,
          border: `1px solid ${surface.border}`,
          backgroundColor: surface.tint,
        }}
      >
        <Box sx={{ p: { xs: 3, md: 4 } }}>
          <Stack spacing={3}>
            <Stack spacing={1.5} sx={{ maxWidth: 680 }}>
              <Typography
                variant="overline"
                sx={{ letterSpacing: "0.2em", color: "text.secondary" }}
              >
                {t("landing.logo")}
              </Typography>
              <Typography
                variant="h3"
                component="h1"
                sx={{
                  fontSize: { xs: "2rem", md: "2.75rem" },
                  lineHeight: 1.15,
                  fontWeight: 600,
                }}
              >
                {t("landing.title")}
              </Typography>
              <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 640 }}>
                {t("landing.subtitle")}
              </Typography>
            </Stack>

            <Box
              sx={{
                display: "grid",
                gap: 2,
                gridTemplateColumns: { xs: "1fr", md: "repeat(3, minmax(0, 1fr))" },
              }}
            >
              {valuePropKeys.map((key) => (
                <SurfaceCard
                  key={key}
                  padding={3}
                  sx={{
                    height: "100%",
                    borderRadius: 3,
                    backgroundColor: surface.tintStrong,
                    border: `1px solid ${surface.borderSubtle}`,
                  }}
                >
                  <Stack spacing={1.5}>
                    <Typography variant="h6" component="h2">
                      {t(`landing.valueProps.${key}.title`)}
                    </Typography>
                    <Typography color="text.secondary">
                      {t(`landing.valueProps.${key}.body`)}
                    </Typography>
                  </Stack>
                </SurfaceCard>
              ))}
            </Box>
          </Stack>
        </Box>
      </SurfaceCard>
    </PageContainer>
  );
}
