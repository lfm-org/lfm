import { Box, Chip, Typography } from "@mui/material";
import { classColor } from "../../../lib/wow/classColors";
import { getAttendanceConfig } from "../../runs/lib/attendanceConfig";
import SpecIcon from "../../../lib/wow/SpecIcon";

interface CharacterCardProps {
  characterName: string;
  characterClassId: number;
  characterClassName: string;
  specName: string | null;
  specIconUrl: string | null;
  desiredAttendance: string;
}

export default function CharacterCard({
  characterName,
  characterClassId,
  characterClassName,
  specName,
  specIconUrl,
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
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, minWidth: 0 }}>
        {specName && (
          <SpecIcon
            specName={specName}
            wowClassName={characterClassName}
            iconUrl={specIconUrl}
          />
        )}
        <Typography variant="body2" fontWeight={600} lineHeight={1.2} noWrap>
          {characterName}
        </Typography>
      </Box>
      <Chip
        label={attendance.label}
        size="small"
        sx={attendance.chipSx}
      />
    </Box>
  );
}
