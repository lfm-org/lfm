import type { ReactNode } from "react";
import { Box, Chip, Typography } from "@mui/material";
import { DateUtils } from "../../../util/dateUtil";
import type { Raid } from "../lib/raidTypes";
import SurfaceCard from "../../../components/SurfaceCard";

interface RaidInfoCardProps {
  raid: Raid;
  modeLabel: string;
  children?: ReactNode;
}

export default function RaidInfoCard({ raid, modeLabel, children }: RaidInfoCardProps) {
  const isClosed = new Date(raid.signupCloseTime) < new Date();

  return (
    <SurfaceCard sx={{ mb: 2 }}>
      <Box sx={{ display: "flex", alignItems: "baseline", gap: 1, flexWrap: "wrap", mb: 0.5 }}>
        <Typography component="h2" variant="h6" fontWeight={700}>
          {raid.instanceName}
        </Typography>
        <Chip label={modeLabel} size="small" variant="outlined" />
      </Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1, fontStyle: "italic" }}>
        &ldquo;{raid.description}&rdquo;
      </Typography>
      <Typography variant="body2">
        {DateUtils.FormatDateWithPassed(raid.startTime)}
      </Typography>
      <Typography variant="caption" color={isClosed ? "error" : "text.secondary"}>
        Signups {isClosed ? "closed" : "close"}: {DateUtils.FormatDate(raid.signupCloseTime)}
      </Typography>
      {children}
    </SurfaceCard>
  );
}
