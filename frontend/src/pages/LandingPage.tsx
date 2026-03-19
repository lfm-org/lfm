import { Box, Button, Container, Paper, Stack, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router";

const valueProps = [
  {
    title: "Plan raids without message archaeology",
    body: "Keep one schedule, one signup surface, and one answer for who is actually coming.",
  },
  {
    title: "See roster health fast",
    body: "Get role splits and signup status without bouncing between Discord threads and spreadsheets.",
  },
  {
    title: "Keep Battle.net identity in the loop",
    body: "Sign in once, pick a character, and keep raid updates tied to the player profile you use in-game.",
  },
];

export default function LandingPage() {
  return (
    <Box
      sx={{
        px: 2,
        py: { xs: 4, md: 8 },
      }}
    >
      <Container maxWidth="lg">
        <Paper
          elevation={0}
          sx={{
            overflow: "hidden",
            borderRadius: 4,
            border: "1px solid rgba(255, 255, 255, 0.08)",
            backgroundImage: `
              radial-gradient(circle at top right, rgba(144, 202, 249, 0.18), transparent 32%),
              linear-gradient(135deg, rgba(255, 255, 255, 0.04), rgba(255, 255, 255, 0.02))
            `,
          }}
        >
          <Box sx={{ p: { xs: 4, md: 6 } }}>
            <Stack spacing={4}>
              <Stack spacing={2} sx={{ maxWidth: 720 }}>
                <Typography
                  variant="overline"
                  sx={{ letterSpacing: "0.2em", color: "text.secondary" }}
                >
                  SISU RAIDCAL
                </Typography>
                <Typography
                  variant="h2"
                  component="h1"
                  sx={{
                    fontSize: { xs: "2.4rem", md: "4rem" },
                    lineHeight: 1.05,
                    fontWeight: 700,
                  }}
                >
                  Keep Raid Planning Out Of Discord Scrollback
                </Typography>
                <Typography variant="h6" color="text.secondary" sx={{ maxWidth: 640 }}>
                  Sisu Raidcal gives your group one place to publish raids, collect signups,
                  and see whether the roster is actually raid-ready before invite time.
                </Typography>
              </Stack>

              <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                <Button
                  component={RouterLink}
                  to="/login?redirect=%2Fraids"
                  variant="contained"
                  size="large"
                >
                  Sign In To Plan Raids
                </Button>
                <Button
                  component={RouterLink}
                  to="/login"
                  variant="text"
                  size="large"
                >
                  Battle.net Login
                </Button>
              </Stack>

              <Box
                sx={{
                  display: "grid",
                  gap: 2,
                  gridTemplateColumns: { xs: "1fr", md: "repeat(3, minmax(0, 1fr))" },
                }}
              >
                {valueProps.map((valueProp) => (
                  <Paper
                    key={valueProp.title}
                    elevation={0}
                    sx={{
                      p: 3,
                      height: "100%",
                      borderRadius: 3,
                      backgroundColor: "rgba(255, 255, 255, 0.03)",
                      border: "1px solid rgba(255, 255, 255, 0.06)",
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
                  </Paper>
                ))}
              </Box>
            </Stack>
          </Box>
        </Paper>
      </Container>
    </Box>
  );
}
