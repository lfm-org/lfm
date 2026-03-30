import { Box } from "@mui/material";
import { useEffect, useState, type ReactNode } from "react";
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
  const [character, setCharacter] = useState<CharacterData | null>(null);

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
        Skip to main content
      </Box>
      <NavBar character={character} />
      <Box component="main" id="main-content" sx={{ flex: 1 }}>
        {children}
      </Box>
      <Footer />
    </Box>
  );
}
