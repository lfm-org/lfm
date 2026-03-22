import { Box, Typography } from "@mui/material";
import RosterSection from "./RosterSection";
import NotAttendingSection from "./NotAttendingSection";
import type { RaidRole, RaidSignup } from "../lib/raidTypes";

const ROLES: RaidRole[] = ["TANK", "HEALER", "DPS"];

interface RaidRosterGridProps {
  signups: RaidSignup[];
}

export default function RaidRosterGrid({ signups }: RaidRosterGridProps) {
  const attending = signups.filter(s => s.desiredAttendance === "IN");
  const notAttending = signups.filter(s => s.desiredAttendance !== "IN");

  if (signups.length === 0) {
    return (
      <Box role="region" aria-label="Raid roster">
        <Typography variant="body2" color="text.disabled" sx={{ fontStyle: "italic" }}>
          No signups yet
        </Typography>
      </Box>
    );
  }

  return (
    <Box role="region" aria-label="Raid roster" sx={{ display: "flex", flexDirection: "column", gap: 3 }}>
      {attending.length > 0 && (
        <Box>
          <Typography
            component="h2"
            variant="h6"
            fontWeight={700}
            sx={{ mb: 1, textTransform: "uppercase", letterSpacing: "0.05em", color: "text.secondary" }}
          >
            Attending ({attending.length})
          </Typography>
          <Box
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
                signups={attending.filter((signup) => (signup.role ?? "DPS") === role)}
              />
            ))}
          </Box>
        </Box>
      )}
      <NotAttendingSection signups={notAttending} />
    </Box>
  );
}
