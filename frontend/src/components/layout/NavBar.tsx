import {
  AppBar,
  Avatar,
  Box,
  Button,
  CircularProgress,
  ListItemIcon,
  Menu,
  MenuItem,
  Toolbar,
  Tooltip,
  Typography,
} from "@mui/material";
import { useState, type MouseEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink, useLocation, useNavigate } from "react-router";
import { useAuth } from "../../features/auth";
import { logout } from "../../lib/auth";
import Logo from "../Logo";
import {
  getAccountMenuRouteItems,
  getLoginHref,
  getPrimaryNavItems,
} from "./navBarModel";

interface NavBarCharacter {
  name: string;
  portraitUrl: string | null;
}

interface NavBarProps {
  character?: NavBarCharacter | null;
}

export default function NavBar({ character = null }: NavBarProps) {
  const { t } = useTranslation();
  const location = useLocation();
  const navigate = useNavigate();
  const { clearAuth, user } = useAuth();
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);
  const [loggingOut, setLoggingOut] = useState(false);

  const isSiteAdmin = Boolean(user?.isSiteAdmin);
  const accountMenuRouteItems = getAccountMenuRouteItems(isSiteAdmin);
  const primaryNavItems = getPrimaryNavItems();
  const loginHref = getLoginHref(location.pathname, location.search);

  function isActive(path: string): boolean {
    return location.pathname === path || location.pathname.startsWith(path + "/");
  }
  const menuOpen = Boolean(menuAnchor);
  const menuButtonLabel = t("nav.menuLabel", { name: character?.name ?? t("nav.yourAccount") });

  function openMenu(event: MouseEvent<HTMLButtonElement>) {
    setMenuAnchor(event.currentTarget);
  }

  function closeMenu() {
    setMenuAnchor(null);
  }

  async function handleLogout() {
    setLoggingOut(true);
    try {
      await logout();
    } finally {
      closeMenu();
      clearAuth();
      navigate("/login");
    }
  }

  return (
    <AppBar position="static" color="inherit">
      <Toolbar variant="dense">
        <Logo title={t("nav.logo")} />

        {character ? (
          <>
            <Box
              component="nav"
              aria-label={t("nav.primaryNav")}
              sx={{ display: "flex", ml: 2 }}
            >
              {primaryNavItems.map((item) => (
                <Button
                  key={item.to}
                  component={RouterLink}
                  to={item.to}
                  sx={{
                    color: isActive(item.to) ? "primary.main" : "text.secondary",
                    borderBottom: isActive(item.to) ? "2px solid" : "2px solid transparent",
                    borderColor: isActive(item.to) ? "primary.main" : "transparent",
                    borderRadius: 0,
                    px: 1.5,
                  }}
                >
                  {t(item.i18nKey)}
                </Button>
              ))}
            </Box>

            <Tooltip title={character.name} enterDelay={300}>
            <Button
              id="navbar-account-trigger"
              aria-controls={menuOpen ? "navbar-account-menu" : undefined}
              aria-expanded={menuOpen ? "true" : "false"}
              aria-haspopup="menu"
              aria-label={menuButtonLabel}
              color="inherit"
              onClick={openMenu}
              sx={{
                ml: "auto",
                minWidth: 0,
                display: "inline-flex",
                alignItems: "center",
                gap: 1,
              }}
            >
              {character.portraitUrl ? (
                <Avatar
                  alt=""
                  src={character.portraitUrl}
                  sx={{ width: 32, height: 32 }}
                />
              ) : (
                <Avatar sx={{ width: 32, height: 32 }}>
                  {character.name.slice(0, 1).toUpperCase()}
                </Avatar>
              )}
              <Typography
                component="span"
                noWrap
                sx={{ maxWidth: { xs: 88, sm: 148 } }}
              >
                {character.name}
              </Typography>
            </Button>
            </Tooltip>

            <Menu
              id="navbar-account-menu"
              anchorEl={menuAnchor}
              open={menuOpen}
              onClose={closeMenu}
              MenuListProps={{
                "aria-labelledby": "navbar-account-trigger",
              }}
              anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
              transformOrigin={{ vertical: "top", horizontal: "right" }}
            >
              {accountMenuRouteItems.map((item) => (
                <MenuItem
                  key={item.to}
                  component={RouterLink}
                  to={item.to}
                  onClick={closeMenu}
                >
                  {t(item.i18nKey)}
                </MenuItem>
              ))}
              <MenuItem onClick={handleLogout} disabled={loggingOut}>
                {loggingOut && (
                  <ListItemIcon sx={{ minWidth: 28 }}>
                    <CircularProgress size={16} color="inherit" />
                  </ListItemIcon>
                )}
                {t("nav.logout")}
              </MenuItem>
            </Menu>
          </>
        ) : (
          <Box sx={{ ml: "auto" }}>
            <Button
              component={RouterLink}
              to={loginHref}
              color="inherit"
            >
              <Typography component="span">{t("nav.login")}</Typography>
            </Button>
          </Box>
        )}
      </Toolbar>
    </AppBar>
  );
}
