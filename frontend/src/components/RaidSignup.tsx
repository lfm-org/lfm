import { useEffect, useMemo, useState } from "react";
import {
  Box, Typography, Button, Alert, CircularProgress,
  FormControl, InputLabel, Select, MenuItem,
  ToggleButtonGroup, ToggleButton,
} from "@mui/material";
import { Link } from "react-router";
import { useAuth } from "../lib/AuthContext";
import api from "../lib/api";

interface Character {
  id: string;
  name: string;
  realm: string;
  level: number;
  classId: number;
  raceId: number;
  portraitUrl: string;
  fetchedAt?: string;
}

interface CharactersResponse {
  characters: Character[];
  selectedCharacterId: string | null;
}

interface RaidCharacter {
  id: string;
  characterId: string;
  characterName: string;
  characterRealm: string;
  characterLevel: number;
  characterClassName: string;
  characterRaceName: string;
  raiderBattleNetId: string;
  desiredAttendance: string;
  reviewedAttendance: string;
}

interface Raid {
  id: string;
  startTime: string;
  description: string;
  mode: string;
  instanceName: string;
  raidCharacters: RaidCharacter[];
}

type AttendanceStatus = "YES" | "IF_ROOM" | "NO";

const ATTENDANCE_OPTIONS: { value: AttendanceStatus; label: string }[] = [
  { value: "YES", label: "Yes" },
  { value: "IF_ROOM", label: "If Room" },
  { value: "NO", label: "No" },
];

function formatAttendance(value: string): string {
  return value
    .replaceAll("_", " ")
    .split(" ")
    .map((word) => word[0].toUpperCase() + word.slice(1).toLowerCase())
    .join(" ");
}

interface RaidSignupProps {
  raid: Raid;
  onRaidUpdate: (raid: Raid) => void;
}

export default function RaidSignup({ raid, onRaidUpdate }: RaidSignupProps) {
  const { user } = useAuth();

  const [characters, setCharacters] = useState<Character[]>([]);
  const [selectedCharacterId, setSelectedCharacterId] = useState<string | null>(null);
  const [loadingChars, setLoadingChars] = useState(true);

  const [characterId, setCharacterId] = useState("");
  const [attendance, setAttendance] = useState<AttendanceStatus>("YES");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [mode, setMode] = useState<"form" | "view" | "edit">("form");

  const existingSignup = useMemo(
    () => user ? raid.raidCharacters.find((rc) => rc.raiderBattleNetId === user.battleNetId) : undefined,
    [raid.raidCharacters, user?.battleNetId]
  );

  // Fetch characters on mount
  useEffect(() => {
    api.get<CharactersResponse>("/raider/characters")
      .then((res) => {
        setCharacters(res.data.characters);
        setSelectedCharacterId(res.data.selectedCharacterId);
      })
      .catch(() => setError("Failed to load characters"))
      .finally(() => setLoadingChars(false));
  }, []);

  // Set initial mode based on existing signup
  useEffect(() => {
    if (existingSignup) {
      setMode("view");
    } else {
      setMode("form");
    }
  }, [existingSignup]);

  // Default character selection when characters load
  useEffect(() => {
    if (!characterId && characters.length > 0) {
      if (existingSignup) {
        const match = characters.find((c) => c.id === existingSignup.characterId);
        setCharacterId(match?.id || selectedCharacterId || characters[0].id);
      } else {
        setCharacterId(selectedCharacterId || characters[0].id);
      }
    }
  }, [characters, selectedCharacterId, existingSignup]);

  // Auth guard — after all hooks
  if (!user) return null;

  const handleStartEdit = () => {
    if (existingSignup) {
      const match = characters.find((c) => c.id === existingSignup.characterId);
      setCharacterId(match?.id || selectedCharacterId || characters[0]?.id || "");
      setAttendance(existingSignup.desiredAttendance as AttendanceStatus);
    }
    setError(null);
    setMode("edit");
  };

  const handleCancelEdit = () => {
    setError(null);
    setMode("view");
  };

  const handleSubmit = async () => {
    if (!characterId) return;
    setSubmitting(true);
    setError(null);
    try {
      const res = await api.post<Raid>(`/raids/${raid.id}/signup`, {
        characterId,
        desiredAttendance: attendance,
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
      setCharacterId(selectedCharacterId || characters[0]?.id || "");
      setAttendance("YES");
      setMode("form");
    } catch {
      setError("Failed to cancel signup");
    } finally {
      setSubmitting(false);
    }
  };

  if (loadingChars) {
    return (
      <Box sx={{ mb: 2, display: "flex", alignItems: "center", gap: 1 }}>
        <CircularProgress size={20} />
        <Typography variant="body2">Loading characters...</Typography>
      </Box>
    );
  }

  if (characters.length === 0) {
    return (
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2">
          <Link to="/characters">Add a character</Link> before signing up.
        </Typography>
      </Box>
    );
  }

  // Read-only view
  if (mode === "view" && existingSignup) {
    return (
      <Box sx={{ mb: 2 }}>
        {error && <Alert severity="error" sx={{ mb: 1 }}>{error}</Alert>}
        <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
          <Typography variant="body1">
            Signed up: {existingSignup.characterName} ({existingSignup.characterClassName}) — {formatAttendance(existingSignup.desiredAttendance)}
          </Typography>
          <Button size="small" variant="outlined" onClick={handleStartEdit}>
            Change Signup
          </Button>
          <Button size="small" variant="outlined" color="error" onClick={handleCancelSignup} disabled={submitting}>
            Cancel Signup
          </Button>
        </Box>
      </Box>
    );
  }

  // Form (new signup or editing)
  return (
    <Box sx={{ mb: 2 }}>
      {error && <Alert severity="error" sx={{ mb: 1 }}>{error}</Alert>}
      <Box sx={{ display: "flex", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
        <FormControl size="small" sx={{ minWidth: 200 }}>
          <InputLabel>Character</InputLabel>
          <Select
            value={characterId}
            label="Character"
            onChange={(e) => setCharacterId(e.target.value)}
          >
            {characters.map((c) => (
              <MenuItem key={c.id} value={c.id}>
                {c.name} — {c.realm}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <ToggleButtonGroup
          exclusive
          size="small"
          value={attendance}
          onChange={(_, v: AttendanceStatus | null) => { if (v) setAttendance(v); }}
        >
          {ATTENDANCE_OPTIONS.map((opt) => (
            <ToggleButton key={opt.value} value={opt.value}>
              {opt.label}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>

        <Button
          variant="contained"
          size="small"
          onClick={handleSubmit}
          disabled={submitting || !characterId}
        >
          {submitting ? "Submitting..." : existingSignup ? "Update" : "Sign Up"}
        </Button>

        {mode === "edit" && (
          <Button size="small" onClick={handleCancelEdit}>
            Cancel
          </Button>
        )}
      </Box>
    </Box>
  );
}
