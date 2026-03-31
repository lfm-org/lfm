import { useEffect } from "react";
import { Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import PageContainer from "../../../components/layout/PageContainer";
import SurfaceCard from "../../../components/SurfaceCard";
import { surface } from "../../../theme";
import ObfuscatedEmail from "../../../components/ObfuscatedEmail";

const sections = [
  { heading: "privacy.controller.heading", bodies: ["privacy.controller.body"] },
  { heading: "privacy.data.heading", bodies: ["privacy.data.body"] },
  { heading: "privacy.cookies.heading", bodies: ["privacy.cookies.body"] },
  { heading: "privacy.thirdParty.heading", bodies: ["privacy.thirdParty.body"] },
  { heading: "privacy.retention.heading", bodies: ["privacy.retention.body"] },
  { heading: "privacy.rights.heading", bodies: ["privacy.rights.body"] },
] as const;

export default function PrivacyPolicyPage() {
  const { t } = useTranslation();

  useEffect(() => {
    const meta = document.createElement("meta");
    meta.name = "robots";
    meta.content = "noindex, nofollow";
    document.head.appendChild(meta);
    return () => { document.head.removeChild(meta); };
  }, []);

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
                  {t(key)}
                </Typography>
              ))}
            </Stack>
          ))}

          <Stack spacing={1}>
            <Typography variant="h6" component="h2">
              {t("privacy.contact.heading")}
            </Typography>
            <Typography variant="body1" color="text.secondary">
              {t("privacy.contact.body")}
            </Typography>
            <ObfuscatedEmail />
          </Stack>
        </Stack>
      </SurfaceCard>

      {/* Honeypot — invisible decoy to divert email scrapers */}
      <a
        href="mailto:contact@example.com"
        aria-hidden="true"
        tabIndex={-1}
        style={{ position: "absolute", left: "-9999px", opacity: 0, pointerEvents: "none" }}
      >
        contact@example.com
      </a>
    </PageContainer>
  );
}
