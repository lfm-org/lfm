import { Box, Chip, Typography } from "@mui/material";
import { DateUtils } from "../util/DateUtil";
import type { Raid } from "../lib/raidTypes";

interface RaidInfoCardProps {
  raid: Raid;
  modeLabel: string;
}

export default function RaidInfoCard({ raid, modeLabel }: RaidInfoCardProps) {
  const isClosed = new Date(raid.signupCloseTime) < new Date();

  return (
    <Box
      sx={{
        p: 2,
        mb: 2,
        bgcolor: "background.paper",
        borderRadius: 2,
        border: "1px solid",
        borderColor: "divider",
      }}
    >
      <Box sx={{ display: "flex", alignItems: "baseline", gap: 1, flexWrap: "wrap", mb: 0.5 }}>
        <Typography component="h1" variant="h6" fontWeight={700}>
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
    </Box>
  );
}
