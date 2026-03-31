import { Box, Button, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
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
  guildTimezone?: string;
  canSignupToGuildRaids: boolean;
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
  guildTimezone,
  canSignupToGuildRaids,
}: RaidListCardProps) {
  const { t } = useTranslation();
  const roleCounts = getRoleCounts(raid);
  const showDetails = !isMobile || isExpanded;

  return (
    <Box
      component="section"
      aria-label={`${raid.instanceName}: ${raid.description}`}
      id={`raid-card-${raid.id}`}
      data-testid="raid-card"
      sx={{ display: "grid", gap: 2, border: "1px solid", borderColor: "divider", borderRadius: 2, p: 2 }}
    >
      <RaidInfoCard raid={raid} modeLabel={modeLabel} guildTimezone={guildTimezone}>
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
              {[
                t("raidList.signups", { count: raid.raidCharacters.length }),
                t("raidList.tanks", { count: roleCounts.TANK }),
                t("raidList.healers", { count: roleCounts.HEALER }),
                t("raidList.dps", { count: roleCounts.DPS }),
              ].join(" · ")}
            </Typography>
            <Button size="small" variant="outlined" onClick={onToggle} sx={{ minHeight: 44 }}>
              {showDetails ? t("raidList.hideDetails") : t("raidList.showDetails")}
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
            guildTimezone={guildTimezone}
            canSignupToGuildRaids={canSignupToGuildRaids}
          />
          <RaidRosterGrid signups={raid.raidCharacters} />
        </>
      )}
    </Box>
  );
}
