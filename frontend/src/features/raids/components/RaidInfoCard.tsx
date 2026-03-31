import type { ReactNode } from "react";
import { Box, Chip, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { DateTime } from "luxon";
import type { Raid } from "../lib/raidTypes";
import SurfaceCard from "../../../components/SurfaceCard";
import { GUILD_TIMEZONE } from "../../../lib/guildConfig";

interface RaidInfoCardProps {
  raid: Raid;
  modeLabel: string;
  guildTimezone?: string;
  children?: ReactNode;
}

function parseRaidTime(iso: string, zone: string): DateTime | null {
  if (!iso) return null;
  const dt = DateTime.fromISO(iso, { zone: "UTC" }).setZone(zone);
  return dt.isValid ? dt : null;
}

export default function RaidInfoCard({ raid, modeLabel, guildTimezone, children }: RaidInfoCardProps) {
  const { t } = useTranslation();
  const timezone = guildTimezone ?? GUILD_TIMEZONE;
  const startDt = parseRaidTime(raid.startTime, timezone);
  const closeDt = parseRaidTime(raid.signupCloseTime, timezone);

  const startDisplay = startDt?.isValid
    ? startDt.setLocale("fi").toLocaleString(DateTime.DATETIME_SHORT)
    : "—";
  const startHasPassed = startDt?.isValid ? startDt < DateTime.now() : false;

  const isClosed = closeDt?.isValid ? closeDt < DateTime.now() : false;

  return (
    <SurfaceCard sx={{ mb: 2 }}>
      <Box sx={{ display: "flex", alignItems: "baseline", gap: 1, flexWrap: "wrap", mb: 0.5 }}>
        <Typography component="h2" variant="h6" fontWeight={700}>
          {raid.instanceName}
        </Typography>
        <Chip label={modeLabel} size="small" variant="outlined" />
        {raid.visibility === "GUILD" && (
          <Chip label={raid.creatorGuild || "Guild"} size="small" color="primary" variant="outlined" />
        )}
      </Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1, fontStyle: "italic" }}>
        &ldquo;{raid.description}&rdquo;
      </Typography>
      <Typography variant="body2" color={startHasPassed ? "text.secondary" : "text.primary"}>
        {startDisplay}{startHasPassed ? ` ${t("raidInfo.passed")}` : ""}
      </Typography>
      {closeDt && (
        <Typography variant="caption" color={isClosed ? "error" : "text.secondary"}>
          {isClosed ? t("raidInfo.signupsClosed") : t("raidInfo.signupsClose")}:{" "}
          {closeDt.setLocale("fi").toLocaleString(DateTime.DATETIME_SHORT)}
        </Typography>
      )}
      {children}
    </SurfaceCard>
  );
}
