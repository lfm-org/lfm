import { Box, type BoxProps } from "@mui/material";
import { layout } from "../../theme";

interface PageContainerProps extends BoxProps {
  maxWidth?: number;
}

export default function PageContainer({ maxWidth = layout.maxWidth, sx, children, ...props }: PageContainerProps) {
  return (
    <Box
      sx={[
        {
          maxWidth,
          mx: "auto",
          px: layout.px,
          py: { xs: layout.py, md: layout.py + 2 },
        },
        ...(Array.isArray(sx) ? sx : sx ? [sx] : []),
      ]}
      {...props}
    >
      {children}
    </Box>
  );
}
