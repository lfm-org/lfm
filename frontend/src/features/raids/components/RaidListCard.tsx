import { Box, Button, Typography } from "@mui/material";
import RaidInfoCard from "./RaidInfoCard";
import RaidRosterGrid from "./RaidRosterGrid";
import RaidSignupCard, { type RaidSignupCharacter } from "./RaidSignupCard";
import type { Raid } from "../lib/raidTypes";

interface RaidListCardProps {
  raid: Raid;
  modeLabel: string;
  isMobile: boolean;
  isExpanded: boolean;
  onToggle: () => void;
  onRaidUpdate: (raid: Raid) => void;
  characters: RaidSignupCharacter[];
  selectedCharacterId: string | null;
  loadingChars: boolean;
  charactersError: string | null;
}

function getRoleCounts(raid: Raid) {
  return raid.raidCharacters.reduce(
    (counts, signup) => {
      counts[signup.role ?? "DPS"] += 1;
      return counts;
    },
    { TANK: 0, HEALER: 0, DPS: 0 }
  );
}

export default function RaidListCard({
  raid,
  modeLabel,
  isMobile,
  isExpanded,
  onToggle,
  onRaidUpdate,
  characters,
  selectedCharacterId,
  loadingChars,
  charactersError,
}: RaidListCardProps) {
  const roleCounts = getRoleCounts(raid);
  const showDetails = !isMobile || isExpanded;

  return (
    <Box
      component="section"
      aria-label={`${raid.instanceName}: ${raid.description}`}
      id={`raid-card-${raid.id}`}
      data-testid="raid-card"
      sx={{ display: "grid", gap: 2 }}
    >
      <RaidInfoCard raid={raid} modeLabel={modeLabel}>
        {isMobile && (
          <Box
            sx={{
              mt: 1.5,
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              gap: 1,
              flexWrap: "wrap",
            }}
          >
            <Typography variant="caption" color="text.secondary">
              {raid.raidCharacters.length} signups · Tanks {roleCounts.TANK} · Healers {roleCounts.HEALER} · DPS {roleCounts.DPS}
            </Typography>
            <Button size="small" variant="outlined" onClick={onToggle} sx={{ minHeight: 44 }}>
              {showDetails ? "Hide details" : "Show details"}
            </Button>
          </Box>
        )}
      </RaidInfoCard>

      {showDetails && (
        <>
          <RaidSignupCard
            raid={raid}
            onRaidUpdate={onRaidUpdate}
            characters={characters}
            selectedCharacterId={selectedCharacterId}
            loadingChars={loadingChars}
            charactersError={charactersError}
          />
          <RaidRosterGrid signups={raid.raidCharacters} />
        </>
      )}
    </Box>
  );
}
