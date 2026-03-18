import { useEffect, useState } from "react";
import { useParams } from "react-router";
import { Box, CircularProgress, Typography } from "@mui/material";
import api from "../lib/api";
import RaidInfoCard from "../components/RaidInfoCard";
import RaidSignupCard from "../components/RaidSignupCard";
import RosterSection from "../components/RosterSection";
import type { Raid, RaidRole } from "../lib/raidTypes";

const ROLES: RaidRole[] = ["TANK", "HEALER", "DPS"];

export default function RaidDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [raid, setRaid] = useState<Raid | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    api.get<Raid>(`/raids/${id}`)
      .then(res => setRaid(res.data))
      .catch(() => setError("Failed to load raid"))
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", mt: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error || !raid) {
    return <Typography color="error" sx={{ m: 2 }}>{error ?? "Raid not found"}</Typography>;
  }

  return (
    <Box sx={{ maxWidth: 1100, mx: "auto", px: 2, py: 2 }}>
      <RaidInfoCard raid={raid} />
      <RaidSignupCard raid={raid} onRaidUpdate={setRaid} />

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", md: "repeat(3, 1fr)" },
          gap: 2,
        }}
      >
        {ROLES.map(role => (
          <RosterSection
            key={role}
            role={role}
            signups={raid.raidCharacters.filter(rc => (rc.role ?? "DPS") === role)}
          />
        ))}
      </Box>
    </Box>
  );
}
