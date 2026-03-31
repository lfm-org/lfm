import { AppBar, Box, Button, Toolbar } from "@mui/material";
import { Link as RouterLink, useLocation, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { logout } from "../../lib/auth";
import { useAuth } from "../../features/auth";
import Logo from "../Logo";

interface NavBarCharacter {
  name: string;
  portraitUrl: string | null;
}

interface NavBarProps {
  character?: NavBarCharacter | null;
}

export default function NavBar({ character = null }: NavBarProps) {
  const location = useLocation();
  const navigate = useNavigate();
  const { clearAuth, user } = useAuth();
  const { t } = useTranslation();
  const handleLogout = async () => {
    try {
      await logout();
    } finally {
      clearAuth();
      navigate("/login");
    }
  };
  const redirectPath = location.pathname === "/" || location.pathname.startsWith("/login")
    ? "/raids"
    : `${location.pathname}${location.search}`;
  const loginHref = `/login?redirect=${encodeURIComponent(redirectPath)}`;

  return (
    <AppBar position="static" color="inherit">
      <Toolbar variant="dense">
        <Logo title={t("nav.logo")} />
        <Button
          component={RouterLink}
          to="/raids"
          color="inherit"
          size="small"
          sx={{ ml: 2 }}
        >
          {t("nav.raids")}
        </Button>
        <Button
          component={RouterLink}
          to="/guild"
          color="inherit"
          size="small"
          sx={{ ml: 0.5 }}
        >
          {t("nav.guild")}
        </Button>
        {character ? (
          <>
            {user?.isSiteAdmin && (
              <Button
                component={RouterLink}
                to="/guild/admin"
                color="inherit"
                size="small"
                sx={{ ml: 0.5 }}
              >
                {t("nav.guildAdmin")}
              </Button>
            )}
            {character.portraitUrl && (
              <Box
                component="img"
                src={character.portraitUrl}
                alt={character.name}
                sx={{
                  width: 32,
                  height: 32,
                  borderRadius: "50%",
                  ml: 1,
                  flexShrink: 0,
                }}
              />
            )}
            <Button
              component={RouterLink}
              to="/characters"
              color="inherit"
              size="small"
              sx={{ ml: 0.5 }}
            >
              {character.name}
            </Button>
            <Button
              onClick={handleLogout}
              color="inherit"
              size="small"
              sx={{ ml: 0.5 }}
            >
              {t("nav.logout")}
            </Button>
          </>
        ) : (
          <Button
            component={RouterLink}
            to={loginHref}
            color="inherit"
            size="small"
            sx={{ ml: 1 }}
          >
            {t("nav.login")}
          </Button>
        )}
      </Toolbar>
    </AppBar>
  );
}
