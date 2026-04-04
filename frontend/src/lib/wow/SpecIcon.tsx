import { Box } from "@mui/material";

interface SpecIconProps {
  specName: string;
  wowClassName: string;
  iconUrl: string | null;
  size?: number;
}

export default function SpecIcon({ specName, wowClassName, iconUrl, size = 22 }: SpecIconProps) {
  const label = `${specName} ${wowClassName}`;

  if (!iconUrl) {
    return (
      <Box
        component="span"
        role="img"
        aria-label={label}
        title={label}
        sx={{
          width: size,
          height: size,
          borderRadius: "4px",
          bgcolor: "action.selected",
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: size * 0.55,
          fontWeight: 700,
          color: "text.secondary",
          flexShrink: 0,
        }}
      >
        {specName.charAt(0)}
      </Box>
    );
  }

  return (
    <Box
      component="img"
      src={iconUrl}
      alt={label}
      title={label}
      sx={{
        width: size,
        height: size,
        borderRadius: "4px",
        flexShrink: 0,
      }}
    />
  );
}
