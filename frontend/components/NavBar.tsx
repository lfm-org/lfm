"use client";

import { AppBar, Button, Toolbar, Typography } from "@mui/material";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Logo } from "./Logo";

interface NavBarProps {
  battleTag?: string | null;
}

export default function NavBar({ battleTag = null }: NavBarProps) {
  const pathname = usePathname();
  const loginHref = `/login?redirect=${encodeURIComponent(pathname)}`;

  return (
    <AppBar position="static" color="inherit">
      <Toolbar variant="dense">
        <Logo image="/favicon.ico" title="PUG ME!" />
        <Button
          component={Link}
          href="/raids"
          color="inherit"
          size="small"
          style={{ marginLeft: "16px" }}
        >
          Raids
        </Button>
        {battleTag ? (
          <Typography
            variant="body2"
            color="inherit"
            style={{ marginLeft: "8px" }}
          >
            {battleTag}
          </Typography>
        ) : (
          <Button
            component={Link}
            href={loginHref}
            color="inherit"
            size="small"
            style={{ marginLeft: "8px" }}
          >
            Login
          </Button>
        )}
      </Toolbar>
    </AppBar>
  );
}
