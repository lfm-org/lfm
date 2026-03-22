import type { ReactNode } from "react";
import { Box, Chip, Typography } from "@mui/material";
import { DateTime } from "luxon";
import type { Raid } from "../lib/raidTypes";
import SurfaceCard from "../../../components/SurfaceCard";
import { GUILD_TIMEZONE } from "../../../lib/guildConfig";

interface RaidInfoCardProps {
  raid: Raid;
  modeLabel: string;
  children?: ReactNode;
}

function parseRaidTime(iso: string): DateTime | null {
  if (!iso) return null;
  const dt = DateTime.fromISO(iso, { zone: "UTC" }).setZone(GUILD_TIMEZONE);
  return dt.isValid ? dt : null;
}

export default function RaidInfoCard({ raid, modeLabel, children }: RaidInfoCardProps) {
  const startDt = parseRaidTime(raid.startTime);
  const closeDt = parseRaidTime(raid.signupCloseTime);

  const startDisplay = startDt
    ? startDt < DateTime.now()
      ? "Passed"
      : startDt.setLocale("fi").toLocaleString(DateTime.DATETIME_SHORT)
    : "—";

  const isClosed = closeDt ? closeDt < DateTime.now() : false;

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
        {startDisplay}
      </Typography>
      {closeDt && (
        <Typography variant="caption" color={isClosed ? "error" : "text.secondary"}>
          Signups {isClosed ? "closed" : "close"}:{" "}
          {closeDt.setLocale("fi").toLocaleString(DateTime.DATETIME_SHORT)}
        </Typography>
      )}
      {children}
    </SurfaceCard>
  );
}
