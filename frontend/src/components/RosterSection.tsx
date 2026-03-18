import { Box, Typography } from "@mui/material";
import CharacterCard from "./CharacterCard";
import type { RaidRole, RaidSignup } from "../lib/raidTypes";

const ROLE_LABEL: Record<RaidRole, string> = {
  TANK: "Tanks",
  HEALER: "Healers",
  DPS: "DPS",
};

interface RosterSectionProps {
  role: RaidRole;
  signups: RaidSignup[];
}

export default function RosterSection({ role, signups }: RosterSectionProps) {
  return (
    <Box>
      <Typography
        variant="subtitle2"
        fontWeight={700}
        sx={{ mb: 1, textTransform: "uppercase", letterSpacing: "0.05em", color: "text.secondary" }}
      >
        {ROLE_LABEL[role]} ({signups.length})
      </Typography>
      {signups.length === 0 ? (
        <Typography variant="body2" color="text.disabled" sx={{ fontStyle: "italic" }}>
          No signups yet
        </Typography>
      ) : (
        signups.map(s => (
          <CharacterCard
            key={s.id}
            characterName={s.characterName}
            characterClassId={s.characterClassId}
            characterClassName={s.characterClassName}
            specName={s.specName}
            desiredAttendance={s.desiredAttendance}
          />
        ))
      )}
    </Box>
  );
}
