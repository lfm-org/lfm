import { Box } from "@mui/material";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useLocation } from "react-router";
import NavBar from "./NavBar";
import Footer from "./Footer";
import { useAuth } from "../../features/auth";
import api from "../../lib/api";
import { normalizePortraitUrlField } from "../../lib/portraitUrls";

interface CharacterInfo {
  id: string;
  name: string;
  portraitUrl: string;
}

interface CharacterData {
  name: string;
  portraitUrl: string;
}

interface Props {
  children: ReactNode;
}

export default function AppLayout({ children }: Props) {
  const { user } = useAuth();
  const { t } = useTranslation();
  const location = useLocation();
  const mainRef = useRef<HTMLElement>(null);
  const isFirstRender = useRef(true);
  const [character, setCharacter] = useState<CharacterData | null>(null);

  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }
    mainRef.current?.focus();
  }, [location.pathname]);

  useEffect(() => {
    if (user?.selectedCharacterId) {
      api.get<{ characters: CharacterInfo[]; selectedCharacterId: string }>("/raider/characters")
        .then(res => {
          const characters = res.data.characters.map((character) => normalizePortraitUrlField(character));
          const selected = characters.find(
            (c: CharacterInfo) => c.id === res.data.selectedCharacterId
          );
          if (selected) setCharacter({ name: selected.name, portraitUrl: selected.portraitUrl });
        })
        .catch(() => {});
    } else {
      setCharacter(null);
    }
  }, [user?.selectedCharacterId]);

  return (
    <Box sx={{ minHeight: "100vh", display: "flex", flexDirection: "column" }}>
      <Box
        component="a"
        href="#main-content"
        sx={{
          position: "absolute",
          left: "-9999px",
          top: "auto",
          width: "1px",
          height: "1px",
          overflow: "hidden",
          "&:focus": {
            position: "fixed",
            top: 8,
            left: 8,
            width: "auto",
            height: "auto",
            overflow: "visible",
            zIndex: 9999,
            bgcolor: "background.paper",
            color: "primary.main",
            px: 2,
            py: 1,
            borderRadius: 1,
            textDecoration: "none",
            fontWeight: 600,
            fontSize: "0.875rem",
            boxShadow: 3,
          },
        }}
      >
        {t("a11y.skipToContent")}
      </Box>
      <NavBar character={character} />
      <Box component="main" id="main-content" ref={mainRef} tabIndex={-1} sx={{ flex: 1, outline: "none" }}>
        {children}
      </Box>
      <Footer />
    </Box>
  );
}
