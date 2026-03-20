import { Box, Button, Stack, Typography } from "@mui/material";
import { useSearchParams } from "react-router";
import { getLoginUrl } from "../../../lib/auth";
import SurfaceCard from "../../../components/SurfaceCard";

export default function LoginPage() {
  const [searchParams] = useSearchParams();
  const redirectPath = searchParams.get("redirect") || "/raids";

  return (
    <Box sx={{ minHeight: "100%", display: "grid", placeItems: "center", px: 2, py: 4 }}>
      <SurfaceCard sx={{ width: "min(100%, 480px)" }}>
        <Stack spacing={2} alignItems="center" textAlign="center">
          <Typography variant="h4" component="h1">
            Sign in with Battle.net
          </Typography>
          <Typography color="text.secondary">
            Continue with your Battle.net account to keep track of raid signups.
          </Typography>
          <Button
            variant="contained"
            color="primary"
            size="large"
            disableElevation
            href={getLoginUrl(redirectPath)}
          >
            Continue with Battle.net
          </Button>
        </Stack>
      </SurfaceCard>
    </Box>
  );
}
