import { Link, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router";

export default function Logo({ title }: { title: string }) {
  return (
    <Link
      component={RouterLink}
      to="/"
      underline="none"
      color="inherit"
      sx={{ flexGrow: 1, display: "inline-flex", alignItems: "center", flexShrink: 0 }}
    >
      <Typography variant="h6" sx={{ mr: { xs: 2, md: 4 }, whiteSpace: "nowrap" }}>
        {title}
      </Typography>
    </Link>
  );
}
