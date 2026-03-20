import { Box } from "@mui/material";
import RosterSection from "./RosterSection";
import type { RaidRole, RaidSignup } from "../lib/raidTypes";

const ROLES: RaidRole[] = ["TANK", "HEALER", "DPS"];

interface RaidRosterGridProps {
  signups: RaidSignup[];
}

export default function RaidRosterGrid({ signups }: RaidRosterGridProps) {
  return (
    <Box
      role="region"
      aria-label="Raid roster"
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "1fr", md: "repeat(3, 1fr)" },
        gap: 2,
      }}
    >
      {ROLES.map((role) => (
        <RosterSection
          key={role}
          role={role}
          signups={signups.filter((signup) => (signup.role ?? "DPS") === role)}
        />
      ))}
    </Box>
  );
}
