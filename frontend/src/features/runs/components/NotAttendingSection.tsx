import { Box, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { CharacterCard } from "../../characters";
import type { RaidSignup } from "../lib/raidTypes";

interface NotAttendingSectionProps {
  signups: RaidSignup[];
}

export default function NotAttendingSection({ signups }: NotAttendingSectionProps) {
  const { t } = useTranslation();

  if (signups.length === 0) return null;

  return (
    <Box>
      <Typography
        component="h2"
        variant="h6"
        fontWeight={700}
        sx={{ mb: 1, textTransform: "uppercase", letterSpacing: "0.05em", color: "text.secondary" }}
      >
        {t("notAttending.title", { count: signups.length })}
      </Typography>
      {signups.map(s => (
        <CharacterCard
          key={s.id}
          characterName={s.characterName}
          characterClassId={s.characterClassId}
          characterClassName={s.characterClassName}
          specName={s.specName}
          desiredAttendance={s.desiredAttendance}
        />
      ))}
    </Box>
  );
}
