import { useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, CircularProgress, FormControl,
  InputLabel, MenuItem, Select, ToggleButton,
  ToggleButtonGroup, Typography,
} from "@mui/material";
import { Link } from "react-router";
import { useAuth } from "../lib/AuthContext";
import api from "../lib/api";
import { ATTENDANCE_OPTIONS, getAttendanceConfig, type AttendanceStatus } from "../lib/attendanceConfig";
import type { Raid } from "../lib/raidTypes";
import SurfaceCard from "./SurfaceCard";

export interface RaidSignupCharacter {
  id: string;
  name: string;
  realm: string;
  classId: number;
  portraitUrl?: string;
  specializations?: Array<{ id: number; name: string; role: string }>;
  activeSpecId?: number | null;
}

interface RaidSignupCardProps {
  raid: Raid;
  onRaidUpdate: (raid: Raid) => void;
  characters: RaidSignupCharacter[];
  selectedCharacterId: string | null;
  loadingChars: boolean;
  charactersError: string | null;
}

const CHARACTER_LABEL_ID = "raid-signup-character-label";
const CHARACTER_SELECT_ID = "raid-signup-character";
const SPEC_LABEL_ID = "raid-signup-spec-label";
const SPEC_SELECT_ID = "raid-signup-spec";

export default function RaidSignupCard({
  raid,
  onRaidUpdate,
  characters,
  selectedCharacterId,
  loadingChars,
  charactersError,
}: RaidSignupCardProps) {
  const { user } = useAuth();
  const [characterId, setCharacterId] = useState("");
  const [specId, setSpecId] = useState<number | null>(null);
  const [attendance, setAttendance] = useState<AttendanceStatus>("IN");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [mode, setMode] = useState<"form" | "view" | "edit">("form");

  const existingSignup = useMemo(
    () => user ? raid.raidCharacters.find(rc => rc.raiderBattleNetId === user.battleNetId) : undefined,
    [raid.raidCharacters, user?.battleNetId]
  );

  const isClosed = new Date(raid.signupCloseTime) < new Date();

  const selectedCharacter = characters.find(c => c.id === characterId);
  const availableSpecs = selectedCharacter?.specializations ?? [];
  const signupRegionProps = {
    component: "section" as const,
    "aria-label": `Your Signup for ${raid.description}`,
  };

  // Set mode based on existing signup
  useEffect(() => {
    setMode(existingSignup ? "view" : "form");
  }, [existingSignup]);

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

  // Update specId when character changes
  const handleCharacterChange = (newCharId: string) => {
    setCharacterId(newCharId);
    const char = characters.find(c => c.id === newCharId);
    setSpecId(char?.activeSpecId ?? char?.specializations?.[0]?.id ?? null);
  };

  if (!user) return null;

  if (loadingChars) {
    return (
      <SurfaceCard
        {...signupRegionProps}
        sx={{ mb: 2, display: "flex", alignItems: "center", gap: 1 }}
      >
        <CircularProgress size={20} />
        <Typography variant="body2">Loading characters...</Typography>
      </SurfaceCard>
    );
  }

  if (charactersError && characters.length === 0) {
    return (
      <SurfaceCard
        {...signupRegionProps}
        tone="error"
        sx={{ mb: 2 }}
      >
        <Alert severity="error">{charactersError}</Alert>
      </SurfaceCard>
    );
  }

  if (characters.length === 0) {
    return (
      <SurfaceCard
        {...signupRegionProps}
        sx={{ mb: 2 }}
      >
        <Typography variant="body2">
          <Link to="/characters">Add a character</Link> before signing up.
        </Typography>
      </SurfaceCard>
    );
  }

  if (isClosed) {
    return (
      <SurfaceCard
        {...signupRegionProps}
        tone="error"
        sx={{ mb: 2 }}
      >
        <Typography variant="body2" color="error">Signups are closed.</Typography>
      </SurfaceCard>
    );
  }

  const handleStartEdit = () => {
    if (existingSignup) {
      const match = characters.find(c => c.id === existingSignup.characterId);
      const char = match ?? characters[0];
      setCharacterId(char.id);
      setSpecId(existingSignup.specId ?? char.activeSpecId ?? null);
      setAttendance(existingSignup.desiredAttendance as AttendanceStatus);
    }
    setError(null);
    setMode("edit");
  };

  const handleCancelEdit = () => { setError(null); setMode("view"); };

  const handleSubmit = async () => {
    if (!characterId) return;
    setSubmitting(true);
    setError(null);
    try {
      const res = await api.post<Raid>(`/raids/${raid.id}/signup`, {
        characterId,
        desiredAttendance: attendance,
        specId,
      });
      onRaidUpdate(res.data);
      setMode("view");
    } catch {
      setError("Failed to submit signup");
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancelSignup = async () => {
    if (!confirm("Cancel your signup for this raid?")) return;
    setSubmitting(true);
    setError(null);
    try {
      const res = await api.delete<Raid>(`/raids/${raid.id}/signup`);
      onRaidUpdate(res.data);
      setMode("form");
    } catch {
      setError("Failed to cancel signup");
    } finally {
      setSubmitting(false);
    }
  };

  const cardSx = { mb: 2 };

  // Read-only view
  if (mode === "view" && existingSignup) {
    const cfg = getAttendanceConfig(existingSignup.desiredAttendance);
    return (
      <SurfaceCard {...signupRegionProps} sx={cardSx}>
        {error && <Alert severity="error" sx={{ mb: 1 }}>{error}</Alert>}
        <Box sx={{ display: "flex", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
          <Typography variant="body2">
            <strong>{existingSignup.characterName}</strong>
            {existingSignup.specName ? ` · ${existingSignup.specName}` : ""}
          </Typography>
          <Box
            component="span"
            sx={{ px: 1.5, py: 0.25, borderRadius: 1, fontSize: "0.75rem", fontWeight: 700, ...cfg.chipSx }}
          >
            {cfg.label}
          </Box>
          <Button size="small" variant="outlined" onClick={handleStartEdit}>Change</Button>
          <Button size="small" variant="outlined" color="error" onClick={handleCancelSignup} disabled={submitting}>
            Cancel Signup
          </Button>
        </Box>
      </SurfaceCard>
    );
  }

  // Form (new signup or editing)
  return (
    <SurfaceCard {...signupRegionProps} sx={cardSx}>
      {charactersError && <Alert severity="error" sx={{ mb: 1 }}>{charactersError}</Alert>}
      {error && <Alert severity="error" sx={{ mb: 1 }}>{error}</Alert>}
      <Box sx={{ display: "flex", alignItems: "flex-start", gap: 2, flexWrap: "wrap" }}>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel id={CHARACTER_LABEL_ID}>Character</InputLabel>
          <Select
            id={CHARACTER_SELECT_ID}
            labelId={CHARACTER_LABEL_ID}
            value={characterId}
            label="Character"
            onChange={e => handleCharacterChange(e.target.value)}
          >
            {characters.map(c => (
              <MenuItem key={c.id} value={c.id}>{c.name} — {c.realm}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl size="small" sx={{ minWidth: 140 }} disabled={availableSpecs.length === 0}>
          <InputLabel id={SPEC_LABEL_ID}>Spec</InputLabel>
          <Select
            id={SPEC_SELECT_ID}
            labelId={SPEC_LABEL_ID}
            value={specId ?? ""}
            label="Spec"
            onChange={e => setSpecId(Number(e.target.value))}
          >
            {availableSpecs.length === 0
              ? <MenuItem value="" disabled>Unknown spec</MenuItem>
              : availableSpecs.map(s => (
                  <MenuItem key={s.id} value={s.id}>{s.name}</MenuItem>
                ))
            }
          </Select>
        </FormControl>

        <ToggleButtonGroup
          exclusive
          size="small"
          aria-label="Attendance"
          value={attendance}
          onChange={(_, v: AttendanceStatus | null) => { if (v) setAttendance(v); }}
          sx={{ flexWrap: "wrap" }}
        >
          {ATTENDANCE_OPTIONS.map(opt => {
            const cfg = getAttendanceConfig(opt.value);
            const selectedStyle = { bgcolor: cfg.color, color: "#fff" };
            return (
              <ToggleButton
                key={opt.value}
                value={opt.value}
                sx={{
                  "&.Mui-selected": { ...selectedStyle, "&:hover": { ...selectedStyle, filter: "brightness(0.9)" } },
                }}
              >
                {opt.label}
              </ToggleButton>
            );
          })}
        </ToggleButtonGroup>

        <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
          <Button variant="contained" size="small" onClick={handleSubmit} disabled={submitting || !characterId}>
            {submitting ? "Submitting…" : existingSignup ? "Update" : "Sign Up"}
          </Button>
          {mode === "edit" && (
            <Button size="small" onClick={handleCancelEdit}>Cancel</Button>
          )}
        </Box>
      </Box>
    </SurfaceCard>
  );
}
