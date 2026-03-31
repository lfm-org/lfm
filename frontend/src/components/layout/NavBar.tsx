import {
  AppBar,
  Avatar,
  Box,
  Button,
  Menu,
  MenuItem,
  Toolbar,
  Typography,
  useMediaQuery,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { useState, type MouseEvent } from "react";
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
  const location = useLocation();
  const navigate = useNavigate();
  const theme = useTheme();
  const isCompact = useMediaQuery(theme.breakpoints.down("md"));
  const { clearAuth, user } = useAuth();
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);

  const isSiteAdmin = Boolean(user?.isSiteAdmin);
  const primaryNavItems = getPrimaryNavItems(isSiteAdmin);
  const accountMenuRouteItems = getAccountMenuRouteItems({
    isSiteAdmin,
    isCompact,
  });
  const loginHref = getLoginHref(location.pathname, location.search);
  const menuOpen = Boolean(menuAnchor);
  const menuButtonLabel = `Open navigation menu for ${character?.name ?? "your account"}`;

  function openMenu(event: MouseEvent<HTMLButtonElement>) {
    setMenuAnchor(event.currentTarget);
  }

  function closeMenu() {
    setMenuAnchor(null);
  }

  async function handleLogout() {
    closeMenu();
    try {
      await logout();
    } finally {
      clearAuth();
      navigate("/login");
    }
  }

  return (
    <AppBar position="static" color="inherit">
      <Toolbar variant="dense">
        <Logo title="🌀 LFM" />

        {character ? (
          <>
            {!isCompact && (
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  ml: 1.5,
                }}
              >
                {primaryNavItems.map((item) => (
                  <Button
                    key={item.to}
                    component={RouterLink}
                    to={item.to}
                    color="inherit"
                    size="small"
                    sx={{ minWidth: 0, px: 1.5 }}
                  >
                    {item.label}
                  </Button>
                ))}
              </Box>
            )}

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
                textTransform: "none",
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
                  {item.label}
                </MenuItem>
              ))}
              <MenuItem onClick={handleLogout}>Logout</MenuItem>
            </Menu>
          </>
        ) : (
          <>
            {!isCompact &&
              primaryNavItems.map((item) => (
                <Button
                  key={item.to}
                  component={RouterLink}
                  to={item.to}
                  color="inherit"
                  size="small"
                  sx={{ ml: 1 }}
                >
                  {item.label}
                </Button>
              ))}
            <Button
              component={RouterLink}
              to={loginHref}
              color="inherit"
              size="small"
              sx={{ ml: 1, textTransform: "none" }}
            >
              Login
            </Button>
          </>
        )}
      </Toolbar>
    </AppBar>
  );
}
