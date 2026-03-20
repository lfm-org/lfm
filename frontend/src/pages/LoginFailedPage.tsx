import { Box, Button, Stack, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router";
import SurfaceCard from "../components/SurfaceCard";

export default function LoginFailedPage() {
  return (
    <Box sx={{ minHeight: "100%", display: "grid", placeItems: "center", px: 2, py: 4 }}>
      <SurfaceCard sx={{ width: "min(100%, 480px)" }}>
        <Stack spacing={2} alignItems="center" textAlign="center">
          <Typography variant="h5" component="h1">
            Sign in failed
          </Typography>
          <Typography color="text.secondary">
            Something went wrong during Battle.net authentication. Please try again.
          </Typography>
          <Button
            component={RouterLink}
            to="/login"
            variant="outlined"
            color="primary"
          >
            Retry login
          </Button>
        </Stack>
      </SurfaceCard>
    </Box>
  );
}
