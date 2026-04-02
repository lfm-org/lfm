import { useState, type ReactNode } from "react";
import { Box, Button, Chip, Tooltip, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { DateTime } from "luxon";
import type { Run } from "../lib/runTypes";
import SurfaceCard from "../../../components/SurfaceCard";
import { GUILD_TIMEZONE } from "../../../lib/guildConfig";
import RunDeleteDialog from "./RunDeleteDialog";
import { isEditingClosed } from "../lib/runEditability";

interface RunInfoCardProps {
  run: Run;
  modeLabel: string;
  guildTimezone?: string;
  currentBattleNetId?: string | null;
  canDeleteGuildRuns?: boolean;
  canCreateGuildRuns?: boolean;
  onRunDelete?: (runId: string) => void;
  onRunEdit?: (runId: string) => void;
  children?: ReactNode;
}

function parseRunTime(iso: string, zone: string): DateTime | null {
  if (!iso) return null;
  const dt = DateTime.fromISO(iso, { zone: "UTC" }).setZone(zone);
  return dt.isValid ? dt : null;
}

export default function RunInfoCard({ run, modeLabel, guildTimezone, currentBattleNetId, canDeleteGuildRuns, canCreateGuildRuns, onRunDelete, onRunEdit, children }: RunInfoCardProps) {
  const { t } = useTranslation();
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const timezone = guildTimezone ?? GUILD_TIMEZONE;
  const startDt = parseRunTime(run.startTime, timezone);
  const closeDt = parseRunTime(run.signupCloseTime, timezone);

  const canDelete = currentBattleNetId != null && onRunDelete != null && (
    run.creatorBattleNetId === currentBattleNetId ||
    (run.visibility === "GUILD" && canDeleteGuildRuns === true)
  );
  const canEdit = currentBattleNetId != null && onRunEdit != null && (
    run.creatorBattleNetId === currentBattleNetId ||
    (run.visibility === "GUILD" && canCreateGuildRuns === true)
  );
  const editDisabled = canEdit && isEditingClosed(run.signupCloseTime, run.startTime);

  const startDisplay = startDt?.isValid
    ? startDt.setLocale("fi").toLocaleString(DateTime.DATETIME_SHORT)
    : "—";
  const startHasPassed = startDt?.isValid ? startDt < DateTime.now() : false;

  const isClosed = closeDt?.isValid ? closeDt < DateTime.now() : false;

  return (
    <SurfaceCard sx={{ mb: 2 }}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 0.5 }}>
        <Typography component="h2" variant="h6" fontWeight={700}>
          {run.instanceName}
        </Typography>
        <Chip label={modeLabel} size="small" variant="outlined" />
        {run.visibility === "GUILD" && (
          <Chip label={run.creatorGuild || "Guild"} size="small" color="primary" variant="outlined" />
        )}
        {(canEdit || canDelete) && (
          <Box sx={{ display: "flex", gap: 0.5, ml: "auto" }}>
            {canEdit && (
              editDisabled ? (
                <Tooltip title={t("runInfo.editingClosed")}>
                  <span>
                    <Button size="small" disabled sx={{ minWidth: 0, minHeight: 32, py: 0.5, px: 1 }}>
                      {t("runInfo.editButton")}
                    </Button>
                  </span>
                </Tooltip>
              ) : (
                <Button
                  size="small"
                  onClick={() => onRunEdit(run.id)}
                  sx={{ minWidth: 0, minHeight: 32, py: 0.5, px: 1 }}
                >
                  {t("runInfo.editButton")}
                </Button>
              )
            )}
            {canDelete && (
              <Button
                size="small"
                color="error"
                onClick={() => setDeleteDialogOpen(true)}
                sx={{ minWidth: 0, minHeight: 32, py: 0.5, px: 1 }}
              >
                {t("runInfo.deleteButton")}
              </Button>
            )}
          </Box>
        )}
      </Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1, fontStyle: "italic" }}>
        &ldquo;{run.description}&rdquo;
      </Typography>
      <Typography variant="body2" color={startHasPassed ? "text.secondary" : "text.primary"}>
        {startDisplay}{startHasPassed ? ` ${t("runInfo.passed")}` : ""}
      </Typography>
      {closeDt && (
        <Typography variant="caption" color={isClosed ? "error" : "text.secondary"}>
          {isClosed ? t("runInfo.signupsClosed") : t("runInfo.signupsClose")}:{" "}
          {closeDt.setLocale("fi").toLocaleString(DateTime.DATETIME_SHORT)}
        </Typography>
      )}
      {children}
      {canDelete && (
        <RunDeleteDialog
          open={deleteDialogOpen}
          runId={run.id}
          runName={run.instanceName}
          onClose={() => setDeleteDialogOpen(false)}
          onDeleted={(deletedId) => {
            setDeleteDialogOpen(false);
            onRunDelete(deletedId);
          }}
        />
      )}
    </SurfaceCard>
  );
}
