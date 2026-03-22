import { useEffect, useState } from "react";
import { Box, Button, CircularProgress, Stack, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router";
import PageContainer from "../../../components/layout/PageContainer";
import SurfaceCard from "../../../components/SurfaceCard";
import { useAuth } from "../index";
import api from "../../../lib/api";

const valueProps = [
  {
    title: "Shared schedule",
    body: "Keep upcoming raids and signups in one place.",
  },
  {
    title: "Role coverage",
    body: "See tank, healer, and DPS coverage at a glance.",
  },
  {
    title: "Battle.net sign-in",
    body: "Players sign in with Battle.net and use their saved characters.",
  },
];

interface GuildMotd {
  name: string;
  motd: string;
}

function GuildMotdCard() {
  const [motd, setMotd] = useState<GuildMotd | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<GuildMotd>("/guild/motd")
      .then(res => setMotd(res.data))
      .catch(() => setMotd(null))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <SurfaceCard sx={{ display: "flex", justifyContent: "center", p: 4 }}>
        <CircularProgress size={24} />
      </SurfaceCard>
    );
  }

  return (
    <SurfaceCard
      sx={{
        overflow: "hidden",
        borderRadius: 4,
        border: "1px solid rgba(255, 255, 255, 0.08)",
        backgroundColor: "rgba(255, 255, 255, 0.03)",
      }}
    >
      <Box sx={{ p: { xs: 3, md: 4 } }}>
        <Stack spacing={2}>
          {motd?.name && (
            <Typography variant="h5" component="h1" fontWeight={700}>
              {motd.name}
            </Typography>
          )}
          {motd?.motd ? (
            <Typography color="text.secondary" sx={{ fontStyle: "italic", whiteSpace: "pre-wrap" }}>
              &ldquo;{motd.motd}&rdquo;
            </Typography>
          ) : (
            <Typography color="text.secondary">No message of the day.</Typography>
          )}
          <Box>
            <Button component={RouterLink} to="/raids" variant="contained">
              Go to Raids →
            </Button>
          </Box>
        </Stack>
      </Box>
    </SurfaceCard>
  );
}

export default function LandingPage() {
  const { user } = useAuth();

  if (user) {
    return (
      <PageContainer>
        <GuildMotdCard />
      </PageContainer>
    );
  }

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
        <Box sx={{ p: { xs: 3, md: 4 } }}>
          <Stack spacing={3}>
            <Stack spacing={1.5} sx={{ maxWidth: 680 }}>
              <Typography
                variant="overline"
                sx={{ letterSpacing: "0.2em", color: "text.secondary" }}
              >
                🌀 LFM
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
                Plan raids in one place
              </Typography>
              <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 640 }}>
                Create raids, collect signups, and check roster coverage before invite time.
              </Typography>
            </Stack>

            <Box
              sx={{
                display: "grid",
                gap: 2,
                gridTemplateColumns: { xs: "1fr", md: "repeat(3, minmax(0, 1fr))" },
              }}
            >
              {valueProps.map((valueProp) => (
                <SurfaceCard
                  key={valueProp.title}
                  padding={3}
                  sx={{
                    height: "100%",
                    borderRadius: 3,
                    backgroundColor: "rgba(255, 255, 255, 0.025)",
                    border: "1px solid rgba(255, 255, 255, 0.05)",
                  }}
                >
                  <Stack spacing={1.5}>
                    <Typography variant="h6" component="h2">
                      {valueProp.title}
                    </Typography>
                    <Typography color="text.secondary">
                      {valueProp.body}
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
