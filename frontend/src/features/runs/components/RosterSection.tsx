import { Box, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { CharacterCard } from "../../characters";
import type { RunRole, RunSignup } from "../lib/runTypes";

const ROLE_KEY: Record<RunRole, string> = {
  TANK: "rosterSection.tanks",
  HEALER: "rosterSection.healers",
  DPS: "rosterSection.dps",
};

interface RosterSectionProps {
  role: RunRole;
  signups: RunSignup[];
}

export default function RosterSection({ role, signups }: RosterSectionProps) {
  const { t } = useTranslation();

  return (
    <Box>
      <Typography
        component="h3"
        variant="h6"
        fontWeight={700}
        sx={{ mb: 1, textTransform: "uppercase", letterSpacing: "0.05em", color: "text.secondary" }}
      >
        {t(ROLE_KEY[role], { count: signups.length })}
      </Typography>
      {signups.length === 0 ? (
        <Typography variant="body2" color="text.disabled" sx={{ fontStyle: "italic" }}>
          {t("rosterSection.noSignups")}
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
