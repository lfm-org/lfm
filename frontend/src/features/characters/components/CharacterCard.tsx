import { Box, Chip, Typography } from "@mui/material";
import { classColor } from "../../../lib/wow/classColors";
import { getAttendanceConfig } from "../../raids/lib/attendanceConfig";

interface CharacterCardProps {
  characterName: string;
  characterClassId: number;
  characterClassName: string;
  specName: string | null;
  desiredAttendance: string;
}

export default function CharacterCard({
  characterName,
  characterClassId,
  characterClassName,
  specName,
  desiredAttendance,
}: CharacterCardProps) {
  const color = classColor(characterClassId);
  const attendance = getAttendanceConfig(desiredAttendance);

  return (
    <Box
      sx={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        borderLeft: `4px solid ${color}`,
        pl: 1.5,
        py: 1,
        pr: 1,
        bgcolor: "background.paper",
        borderRadius: "0 4px 4px 0",
        mb: 1,
      }}
    >
      <Box>
        <Typography variant="body2" fontWeight={600} lineHeight={1.2}>
          {characterName}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {specName ? `${characterClassName} · ${specName}` : characterClassName}
        </Typography>
      </Box>
      <Chip
        label={attendance.label}
        size="small"
        sx={{ ...attendance.chipSx, fontWeight: 600, fontSize: "0.7rem" }}
      />
    </Box>
  );
}
