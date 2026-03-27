import { Box, Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";
import PageContainer from "../../../components/layout/PageContainer";

interface GuildRouteShellProps {
  title: string;
  description: string;
  children: ReactNode;
}

export default function GuildRouteShell({ title, description, children }: GuildRouteShellProps) {
  return (
    <PageContainer>
      <Stack spacing={3}>
        <Box sx={{ maxWidth: 760 }}>
          <Typography component="h1" variant="h4" gutterBottom>
            {title}
          </Typography>
          <Typography color="text.secondary">{description}</Typography>
        </Box>

        {children}
      </Stack>
    </PageContainer>
  );
}
