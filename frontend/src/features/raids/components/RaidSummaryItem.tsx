import { Box, Chip, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { DateTime } from "luxon";
import type { Raid } from "../lib/raidTypes";
import { GUILD_TIMEZONE } from "../../../lib/guildConfig";

interface RaidSummaryItemProps {
  raid: Raid;
  modeLabel: string;
  selected: boolean;
  onClick: () => void;
  guildTimezone?: string;
}

export default function RaidSummaryItem({ raid, modeLabel, selected, onClick, guildTimezone }: RaidSummaryItemProps) {
  const { t } = useTranslation();
  const timezone = guildTimezone ?? GUILD_TIMEZONE;
  const startDt = raid.startTime
    ? DateTime.fromISO(raid.startTime, { zone: "UTC" }).setZone(timezone)
    : null;
  const startDisplay = startDt?.isValid
    ? startDt.setLocale("fi").toLocaleString(DateTime.DATETIME_SHORT)
    : "—";
  const inCount = raid.raidCharacters.filter(rc => rc.desiredAttendance === "IN").length;

  return (
    <Box
      component="button"
      onClick={onClick}
      sx={{
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        gap: 0.5,
        width: "100%",
        p: 1.5,
        border: "none",
        borderRadius: 1,
        cursor: "pointer",
        textAlign: "left",
        bgcolor: selected ? "action.selected" : "transparent",
        color: "text.primary",
        "&:hover": { bgcolor: selected ? "action.selected" : "action.hover" },
        transition: "background-color 0.15s",
      }}
    >
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, width: "100%" }}>
        <Typography variant="body2" fontWeight={selected ? 700 : 400} sx={{ flex: 1, minWidth: 0 }} noWrap>
          {raid.instanceName}
        </Typography>
        <Chip label={modeLabel} size="small" variant="outlined" sx={{ flexShrink: 0, height: 20 }} />
        {raid.visibility === "GUILD" && (
          <Chip label={t("raidSummary.guild")} size="small" color="primary" variant="outlined" sx={{ flexShrink: 0, height: 20 }} />
        )}
      </Box>
      <Typography variant="caption" color={selected ? "text.primary" : "text.secondary"}>
        {startDisplay}
      </Typography>
      <Typography variant="caption" color={selected ? "text.primary" : "text.secondary"}>
        {t("raidSummary.attending", { count: inCount })} · {t("raidSummary.total", { count: raid.raidCharacters.length })}
      </Typography>
    </Box>
  );
}
