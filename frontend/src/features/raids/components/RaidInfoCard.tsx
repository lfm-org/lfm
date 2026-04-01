import { useState, type ReactNode } from "react";
import { Box, Button, Chip, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { DateTime } from "luxon";
import type { Raid } from "../lib/raidTypes";
import SurfaceCard from "../../../components/SurfaceCard";
import { GUILD_TIMEZONE } from "../../../lib/guildConfig";
import RaidDeleteDialog from "./RaidDeleteDialog";

interface RaidInfoCardProps {
  raid: Raid;
  modeLabel: string;
  guildTimezone?: string;
  currentBattleNetId?: string | null;
  canDeleteGuildRaids?: boolean;
  onRaidDelete?: (raidId: string) => void;
  children?: ReactNode;
}

function parseRaidTime(iso: string, zone: string): DateTime | null {
  if (!iso) return null;
  const dt = DateTime.fromISO(iso, { zone: "UTC" }).setZone(zone);
  return dt.isValid ? dt : null;
}

export default function RaidInfoCard({ raid, modeLabel, guildTimezone, currentBattleNetId, canDeleteGuildRaids, onRaidDelete, children }: RaidInfoCardProps) {
  const { t } = useTranslation();
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const timezone = guildTimezone ?? GUILD_TIMEZONE;
  const startDt = parseRaidTime(raid.startTime, timezone);
  const closeDt = parseRaidTime(raid.signupCloseTime, timezone);

  const canDelete = currentBattleNetId != null && onRaidDelete != null && (
    raid.creatorBattleNetId === currentBattleNetId ||
    (raid.visibility === "GUILD" && canDeleteGuildRaids === true)
  );

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
        {canDelete && (
          <Button
            size="small"
            color="error"
            onClick={() => setDeleteDialogOpen(true)}
            sx={{ ml: "auto", alignSelf: "center", minWidth: 0, py: 0, px: 1 }}
          >
            {t("raidInfo.deleteButton")}
          </Button>
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
      {canDelete && (
        <RaidDeleteDialog
          open={deleteDialogOpen}
          raidId={raid.id}
          raidName={raid.instanceName}
          onClose={() => setDeleteDialogOpen(false)}
          onDeleted={(deletedId) => {
            setDeleteDialogOpen(false);
            onRaidDelete(deletedId);
          }}
        />
      )}
    </SurfaceCard>
  );
}
