import { Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import PageContainer from "../../../components/layout/PageContainer";
import SurfaceCard from "../../../components/SurfaceCard";

const sections = [
  { heading: "privacy.data.heading", bodies: ["privacy.data.body"] },
  { heading: "privacy.cookies.heading", bodies: ["privacy.cookies.body"] },
  { heading: "privacy.thirdParty.heading", bodies: ["privacy.thirdParty.body"] },
  { heading: "privacy.retention.heading", bodies: ["privacy.retention.body"] },
  { heading: "privacy.contact.heading", bodies: ["privacy.contact.body"] },
] as const;

export default function PrivacyPolicyPage() {
  const { t } = useTranslation();
  return (
    <PageContainer>
      <SurfaceCard
        sx={{
          overflow: "hidden",
          borderRadius: 4,
          border: "1px solid rgba(255, 255, 255, 0.08)",
          backgroundColor: "rgba(255, 255, 255, 0.03)",
        }}
      >
        <Stack spacing={3} sx={{ p: { xs: 3, md: 4 } }}>
          <Stack spacing={1}>
            <Typography variant="h4" component="h1" fontWeight={600}>
              {t("privacy.title")}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t("privacy.lastUpdated")}
            </Typography>
          </Stack>

          {sections.map(({ heading, bodies }) => (
            <Stack key={heading} spacing={1}>
              <Typography variant="h6" component="h2">
                {t(heading)}
              </Typography>
              {bodies.map((key) => (
                <Typography key={key} variant="body1" color="text.secondary">
                  {t(key, { contactEmail: import.meta.env.VITE_PRIVACY_EMAIL || "privacy@example.com" })}
                </Typography>
              ))}
            </Stack>
          ))}
        </Stack>
      </SurfaceCard>
    </PageContainer>
  );
}
