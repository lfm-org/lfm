"use client";

import { AppBar, Button, Toolbar } from "@mui/material";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Logo } from "./Logo";

export default function NavBar() {
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
        <Button
          component={Link}
          href={loginHref}
          color="inherit"
          size="small"
          style={{ marginLeft: "8px" }}
        >
          Login
        </Button>
      </Toolbar>
    </AppBar>
  );
}
