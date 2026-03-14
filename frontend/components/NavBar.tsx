"use client";

import { AppBar, Button, Toolbar } from "@mui/material";
import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Logo } from "./Logo";

interface NavBarCharacter {
  name: string;
  portraitUrl: string | null;
}

interface NavBarProps {
  character?: NavBarCharacter | null;
}

export default function NavBar({ character = null }: NavBarProps) {
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
        {character ? (
          <>
            {character.portraitUrl && (
              <Image
                src={character.portraitUrl}
                alt={character.name}
                width={32}
                height={32}
                style={{ borderRadius: "50%", marginLeft: "8px" }}
              />
            )}
            <Button
              component={Link}
              href="/characters"
              color="inherit"
              size="small"
              style={{ marginLeft: "4px" }}
            >
              {character.name}
            </Button>
            <Button
              component={Link}
              href="/api/battlenet/logout"
              color="inherit"
              size="small"
              style={{ marginLeft: "4px" }}
            >
              Logout
            </Button>
          </>
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
