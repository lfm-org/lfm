import { AppBar, Button, Toolbar } from "@mui/material";
import { Link as RouterLink, useLocation } from "react-router";
import { getLogoutUrl } from "../lib/auth";
import { Logo } from "./Logo";

interface NavBarCharacter {
  name: string;
  portraitUrl: string | null;
}

interface NavBarProps {
  character?: NavBarCharacter | null;
}

export default function NavBar({ character = null }: NavBarProps) {
  const location = useLocation();
  const loginHref = `/login?redirect=${encodeURIComponent(location.pathname)}`;

  return (
    <AppBar position="static" color="inherit">
      <Toolbar variant="dense">
        <Logo image="/logo.svg" title="PUG ME!" />
        <Button
          component={RouterLink}
          to="/raids"
          color="inherit"
          size="small"
          style={{ marginLeft: "16px" }}
        >
          Raids
        </Button>
        {character ? (
          <>
            {character.portraitUrl && (
              <img
                src={character.portraitUrl}
                alt={character.name}
                width={32}
                height={32}
                style={{ borderRadius: "50%", marginLeft: "8px" }}
              />
            )}
            <Button
              component={RouterLink}
              to="/characters"
              color="inherit"
              size="small"
              style={{ marginLeft: "4px" }}
            >
              {character.name}
            </Button>
            <Button
              component="a"
              href={getLogoutUrl()}
              color="inherit"
              size="small"
              style={{ marginLeft: "4px" }}
            >
              Logout
            </Button>
          </>
        ) : (
          <Button
            component={RouterLink}
            to={loginHref}
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
