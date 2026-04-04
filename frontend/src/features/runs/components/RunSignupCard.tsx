import { useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, CircularProgress, FormControl,
  InputLabel, MenuItem, Select, ToggleButton,
  ToggleButtonGroup, Typography,
} from "@mui/material";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../auth";
import api from "../../../lib/api";
import { ATTENDANCE_OPTIONS, getAttendanceConfig, type AttendanceStatus } from "../lib/attendanceConfig";
import type { Run } from "../lib/runTypes";
import type { RunSignupCharacter } from "../lib/runSignupCharacters";
import SurfaceCard from "../../../components/SurfaceCard";
import ConfirmDialog from "../../../components/ConfirmDialog";
import { DateTime } from "luxon";
import { GUILD_TIMEZONE } from "../../../lib/guildConfig";
import SpecIcon from "../../../lib/wow/SpecIcon";
import { useSpecIcons } from "../../../lib/wow/useSpecIcons";

export type { RunSignupCharacter } from "../lib/runSignupCharacters";

interface RunSignupCardProps {
  run: Run;
  onRunUpdate: (run: Run) => void;
  characters: RunSignupCharacter[];
  selectedCharacterId: string | null;
  loadingChars: boolean;
  charactersError: string | null;
  guildTimezone?: string;
  canSignupToGuildRuns: boolean;
}

const CHARACTER_LABEL_ID = "run-signup-character-label";
const CHARACTER_SELECT_ID = "run-signup-character";
const SPEC_LABEL_ID = "run-signup-spec-label";
const SPEC_SELECT_ID = "run-signup-spec";

export default function RunSignupCard({
  run,
  onRunUpdate,
  characters,
  selectedCharacterId,
  loadingChars,
  charactersError,
  guildTimezone,
  canSignupToGuildRuns,
}: RunSignupCardProps) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [characterId, setCharacterId] = useState("");
  const [specId, setSpecId] = useState<number | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showCharEdit, setShowCharEdit] = useState(false);
  const [pendingCancel, setPendingCancel] = useState(false);

  const existingSignup = useMemo(
    () => user ? run.runCharacters.find(rc => rc.isCurrentUser) : undefined,
    [run.runCharacters, user]
  );
  const timezone = guildTimezone ?? GUILD_TIMEZONE;
  const { specIcons } = useSpecIcons();

  const closeTime = run.signupCloseTime
    ? DateTime.fromISO(run.signupCloseTime, { zone: "UTC" }).setZone(timezone)
    : null;
  const isClosed = closeTime?.isValid ? closeTime < DateTime.now() : false;
  const guildSignupBlocked = run.visibility === "GUILD" && !canSignupToGuildRuns;

  const selectedCharacter = characters.find(c => c.id === characterId);
  const availableSpecs = selectedCharacter?.specializations ?? [];

  const signupRegionProps = {
    component: "section" as const,
    "aria-label": t("runSignup.signupRegion", { description: run.description }),
  };

  // Default character + spec when characters load
  useEffect(() => {
    if (!characterId && characters.length > 0) {
      const defaultChar = existingSignup
        ? characters.find(c => c.id === existingSignup.characterId) ?? characters[0]
        : characters.find(c => c.id === selectedCharacterId) ?? characters[0];
      setCharacterId(defaultChar.id);
      setSpecId(defaultChar.activeSpecId ?? defaultChar.specializations?.[0]?.id ?? null);
    }
  }, [characters, selectedCharacterId, existingSignup, characterId]);

  // Reset char edit panel when signup changes
  useEffect(() => {
    setShowCharEdit(false);
    setPendingCancel(false);
  }, [existingSignup?.characterId]);

  const handleCharacterChange = (newCharId: string) => {
    setCharacterId(newCharId);
    const char = characters.find(c => c.id === newCharId);
    setSpecId(char?.activeSpecId ?? char?.specializations?.[0]?.id ?? null);
  };

  const handleStatusClick = async (newStatus: AttendanceStatus) => {
    if (submitting) return;
    // No-op if clicking the already-selected status and not changing character
    if (existingSignup && existingSignup.desiredAttendance === newStatus && !showCharEdit) return;
    // Use existing signup's character when not in char-edit mode
    const submitCharId = (existingSignup && !showCharEdit) ? existingSignup.characterId : characterId;
    const submitSpecId = (existingSignup && !showCharEdit) ? existingSignup.specId : specId;
    if (!submitCharId) return;
    setSubmitting(true);
    setError(null);
    try {
      const res = await api.post<Run>(`/runs/${run.id}/signup`, {
        characterId: submitCharId,
        desiredAttendance: newStatus,
        specId: submitSpecId,
      });
      onRunUpdate(res.data);
      setShowCharEdit(false);
    } catch {
      setError(t("runSignup.updateFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancelSignup = async () => {
    setSubmitting(true);
    setError(null);
    try {
      const res = await api.delete<Run>(`/runs/${run.id}/signup`);
      onRunUpdate(res.data);
      setPendingCancel(false);
    } catch {
      setError(t("runSignup.cancelFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  if (!user) return null;

  if (loadingChars) {
    return (
      <SurfaceCard
        {...signupRegionProps}
        sx={{ mb: 2, display: "flex", alignItems: "center", gap: 1 }}
      >
        <CircularProgress size={20} aria-label={t("runSignup.loadingCharacters")} />
        <Typography variant="body2">{t("runSignup.loadingCharacters")}</Typography>
      </SurfaceCard>
    );
  }

  if (charactersError && characters.length === 0) {
    return (
      <SurfaceCard {...signupRegionProps} tone="error" sx={{ mb: 2 }}>
        <Alert severity="error">{charactersError}</Alert>
      </SurfaceCard>
    );
  }

  if (characters.length === 0) {
    return (
      <SurfaceCard {...signupRegionProps} sx={{ mb: 2 }}>
        <Typography variant="body2">
          <Link to="/characters">{t("runSignup.addCharacter")}</Link>{t("runSignup.addCharacterSuffix")}
        </Typography>
      </SurfaceCard>
    );
  }

  if (isClosed) {
    return (
      <SurfaceCard {...signupRegionProps} tone="error" sx={{ mb: 2 }}>
        <Typography variant="body2" color="error">{t("runSignup.signupsClosed")}</Typography>
      </SurfaceCard>
    );
  }

  const currentStatus = existingSignup?.desiredAttendance as AttendanceStatus | undefined;

  return (
    <SurfaceCard {...signupRegionProps} sx={{ mb: 2 }}>
      {error && <Alert severity="error" sx={{ mb: 1 }}>{error}</Alert>}
      <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>

        {/* Character info or selectors */}
        {existingSignup && !showCharEdit ? (
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
              {existingSignup.specName && (
                <SpecIcon
                  specName={existingSignup.specName}
                  wowClassName={existingSignup.characterClassName}
                  iconUrl={existingSignup.specId ? (specIcons.get(existingSignup.specId) ?? null) : null}
                  size={20}
                />
              )}
              <Typography variant="body2">
                <strong>{existingSignup.characterName}</strong>
              </Typography>
            </Box>
            <Button
              size="small"
              variant="text"
              sx={{ p: 0, minWidth: 0, fontSize: "0.75rem" }}
              onClick={() => setShowCharEdit(true)}
            >
              {t("runSignup.changeCharacter")}
            </Button>
          </Box>
        ) : (
          <Box sx={{ display: "flex", alignItems: "flex-start", gap: 2, flexWrap: "wrap" }}>
            <FormControl size="small" sx={{ minWidth: 160 }}>
              <InputLabel id={CHARACTER_LABEL_ID}>{t("runSignup.character")}</InputLabel>
              <Select
                id={CHARACTER_SELECT_ID}
                labelId={CHARACTER_LABEL_ID}
                value={characterId}
                label={t("runSignup.character")}
                onChange={e => handleCharacterChange(e.target.value)}
              >
                {characters.map(c => (
                  <MenuItem key={c.id} value={c.id}>{c.name} — {c.realm}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl size="small" sx={{ minWidth: 140 }} disabled={availableSpecs.length === 0}>
              <InputLabel id={SPEC_LABEL_ID}>{t("runSignup.spec")}</InputLabel>
              <Select
                id={SPEC_SELECT_ID}
                labelId={SPEC_LABEL_ID}
                value={specId ?? ""}
                label={t("runSignup.spec")}
                onChange={e => setSpecId(Number(e.target.value))}
              >
                {availableSpecs.length === 0
                  ? <MenuItem value="" disabled>{t("runSignup.unknownSpec")}</MenuItem>
                  : availableSpecs.map(s => (
                      <MenuItem key={s.id} value={s.id}>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                          <SpecIcon
                            specName={s.name}
                            wowClassName=""
                            iconUrl={specIcons.get(s.id) ?? null}
                            size={16}
                          />
                          {s.name}
                        </Box>
                      </MenuItem>
                    ))
                }
              </Select>
            </FormControl>
            {existingSignup && (
              <Button
                size="small"
                sx={{ alignSelf: "center" }}
                onClick={() => setShowCharEdit(false)}
              >
                {t("runSignup.back")}
              </Button>
            )}
          </Box>
        )}

        {guildSignupBlocked && (
          <Alert severity="info">{t("runSignup.guildRankBlocked")}</Alert>
        )}

        {/* Status buttons + cancel */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
          {!guildSignupBlocked && (
            <ToggleButtonGroup
              exclusive
              size="small"
              aria-label={t("runSignup.attendance")}
              value={currentStatus ?? null}
              sx={{ flexWrap: "wrap" }}
            >
              {ATTENDANCE_OPTIONS.map(opt => {
                const cfg = getAttendanceConfig(opt.value);
                return (
                  <ToggleButton
                    key={opt.value}
                    value={opt.value}
                    disabled={submitting}
                    onClick={() => handleStatusClick(opt.value as AttendanceStatus)}
                    sx={{
                      minWidth: 64,
                      "&.Mui-selected": {
                        bgcolor: cfg.color,
                        color: cfg.textColor,
                        "&:hover": { bgcolor: cfg.color, color: cfg.textColor, filter: "brightness(0.9)" },
                      },
                    }}
                  >
                    {opt.label}
                  </ToggleButton>
                );
              })}
            </ToggleButtonGroup>
          )}

          {submitting && <CircularProgress size={16} />}

          {existingSignup && !pendingCancel && (
            <Button
              size="small"
              variant="outlined"
              color="error"
              disabled={submitting}
              onClick={() => setPendingCancel(true)}
            >
              {t("runSignup.cancel")}
            </Button>
          )}

        </Box>
      </Box>
      <ConfirmDialog
        open={pendingCancel}
        title={t("runSignup.cancelConfirmTitle")}
        description={t("runSignup.cancelConfirmBody")}
        confirmLabel={t("runSignup.cancelConfirmConfirm")}
        cancelLabel={t("runSignup.cancelConfirmCancel")}
        onConfirm={handleCancelSignup}
        onCancel={() => setPendingCancel(false)}
        loading={submitting}
      />
    </SurfaceCard>
  );
}
