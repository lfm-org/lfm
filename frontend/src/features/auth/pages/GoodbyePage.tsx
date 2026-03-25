import { Button, Stack, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router";
import PageContainer from "../../../components/layout/PageContainer";
import SurfaceCard from "../../../components/SurfaceCard";

export default function GoodbyePage() {
  return (
    <PageContainer>
      <SurfaceCard sx={{ maxWidth: 560, mx: "auto" }}>
        <Stack spacing={2} textAlign="center" alignItems="center">
          <Typography variant="h4" component="h1">
            Account deleted
          </Typography>
          <Typography color="text.secondary">
            Your stored raider profile has been removed and your Battle.net session has been cleared.
          </Typography>
          <Typography color="text.secondary">
            Existing raids stay in place, but you no longer have access to the deleted account.
          </Typography>
          <Button component={RouterLink} to="/login" variant="contained">
            Sign in again
          </Button>
        </Stack>
      </SurfaceCard>
    </PageContainer>
  );
}
