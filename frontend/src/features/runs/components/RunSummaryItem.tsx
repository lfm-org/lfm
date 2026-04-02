import { Box, Chip, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { DateTime } from "luxon";
import type { Run } from "../lib/runTypes";
import { isAttending } from "../lib/attendanceConfig";
import { GUILD_TIMEZONE } from "../../../lib/guildConfig";
import { getInstanceTypeColors } from "../../../theme";

interface RunSummaryItemProps {
  run: Run;
  modeLabel: string;
  selected: boolean;
  onClick: () => void;
  guildTimezone?: string;
  passed?: boolean;
  instanceType?: string;
}

export default function RunSummaryItem({ run, modeLabel, selected, onClick, guildTimezone, passed, instanceType }: RunSummaryItemProps) {
  const { t } = useTranslation();
  const typeColors = getInstanceTypeColors(instanceType ?? "UNKNOWN");
  const timezone = guildTimezone ?? GUILD_TIMEZONE;
  const startDt = run.startTime
    ? DateTime.fromISO(run.startTime, { zone: "UTC" }).setZone(timezone)
    : null;
  const startDisplay = startDt?.isValid
    ? startDt.setLocale("fi").toLocaleString(DateTime.DATETIME_SHORT)
    : "—";
  const inCount = run.runCharacters.filter(rc => isAttending(rc.desiredAttendance)).length;

  return (
    <Box
      component="button"
      onClick={onClick}
      aria-current={selected ? "true" : undefined}
      sx={{
        opacity: passed && !selected ? 0.6 : 1,
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        gap: 0.5,
        width: "100%",
        p: 1.5,
        border: "none",
        borderLeft: `3px solid ${typeColors.border}`,
        borderRadius: 1,
        cursor: "pointer",
        textAlign: "left",
        bgcolor: selected ? "action.selected" : "transparent",
        color: "text.primary",
        "&:hover": { bgcolor: selected ? "action.selected" : "action.hover" },
        "&:focus-visible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: 2 },
        transition: "background-color 0.15s",
      }}
    >
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, width: "100%" }}>
        <Typography variant="body2" fontWeight={selected ? 700 : 400} sx={{ flex: 1, minWidth: 0 }} noWrap>
          {run.instanceName}
        </Typography>
        <Chip label={modeLabel} size="small" variant="outlined" sx={{ flexShrink: 0, height: 20 }} />
        {run.visibility === "GUILD" && (
          <Chip label={t("runSummary.guild")} size="small" color="primary" variant="outlined" sx={{ flexShrink: 0, height: 20 }} />
        )}
      </Box>
      <Typography variant="caption" color={selected ? "text.primary" : "text.secondary"}>
        {startDisplay}
      </Typography>
      <Typography variant="caption" color={selected ? "text.primary" : "text.secondary"}>
        {t("runSummary.attending", { count: inCount })} · {t("runSummary.total", { count: run.runCharacters.length })}
      </Typography>
    </Box>
  );
}
