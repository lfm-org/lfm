import { Paper, type PaperProps, type SxProps, type Theme } from "@mui/material";

interface SurfaceCardProps extends PaperProps {
  tone?: "default" | "error";
  padding?: number;
}

function buildSurfaceSx(tone: SurfaceCardProps["tone"], padding: number): SxProps<Theme> {
  return {
    p: padding,
    bgcolor: "background.paper",
    borderRadius: 2,
    borderColor: tone === "error" ? "error.main" : "divider",
  };
}

export default function SurfaceCard({
  tone = "default",
  padding = 2,
  sx,
  ...props
}: SurfaceCardProps) {
  return (
    <Paper
      elevation={0}
      variant="outlined"
      sx={Array.isArray(sx) ? [buildSurfaceSx(tone, padding), ...sx] : [buildSurfaceSx(tone, padding), sx]}
      {...props}
    />
  );
}
