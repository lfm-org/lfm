import { Box, Container, Paper, Stack, Typography } from "@mui/material";

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

export default function LandingPage() {
  return (
    <Box
      sx={{
        px: 2,
        py: { xs: 3, md: 5 },
      }}
    >
      <Container maxWidth="lg">
        <Paper
          elevation={0}
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
                  SISU RAIDCAL
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
                  <Paper
                    key={valueProp.title}
                    elevation={0}
                    sx={{
                      p: 3,
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
