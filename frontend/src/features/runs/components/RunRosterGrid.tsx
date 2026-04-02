import { Box, Typography } from "@mui/material";
import GroupsIcon from "@mui/icons-material/Groups";
import EmptyState from "../../../components/EmptyState";
import { useTranslation } from "react-i18next";
import RosterSection from "./RosterSection";
import NotAttendingSection from "./NotAttendingSection";
import type { RunRole, RunSignup } from "../lib/runTypes";
import { isAttending } from "../lib/attendanceConfig";

const ROLES: RunRole[] = ["TANK", "HEALER", "DPS"];

interface RunRosterGridProps {
  signups: RunSignup[];
}

export default function RunRosterGrid({ signups }: RunRosterGridProps) {
  const { t } = useTranslation();
  const attending = signups.filter(s => isAttending(s.desiredAttendance));
  const notAttending = signups.filter(s => !isAttending(s.desiredAttendance));

  if (signups.length === 0) {
    return (
      <Box role="region" aria-label={t("runRoster.region")}>
        <EmptyState
          icon={<GroupsIcon />}
          message={t("runRoster.noSignups")}
        />
      </Box>
    );
  }

  return (
    <Box role="region" aria-label={t("runRoster.region")} sx={{ display: "flex", flexDirection: "column", gap: 3 }}>
      {attending.length > 0 && (
        <Box>
          <Typography
            component="h2"
            variant="h6"
            fontWeight={700}
            sx={{ mb: 1, textTransform: "uppercase", letterSpacing: "0.05em", color: "text.secondary" }}
          >
            {t("runRoster.attending", { count: attending.length })}
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
