import { Box } from "@mui/material";
import { useEffect, useState, type ReactNode } from "react";
import NavBar from "./NavBar";
import { useAuth } from "../lib/AuthContext";
import api from "../lib/api";

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

export default function Layout({ children }: Props) {
  const { user } = useAuth();
  const [character, setCharacter] = useState<CharacterData | null>(null);

  useEffect(() => {
    if (user?.selectedCharacterId) {
      api.get<{ characters: CharacterInfo[]; selectedCharacterId: string }>("/raider/characters")
        .then(res => {
          const selected = res.data.characters.find(
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
      <NavBar character={character} />
      <Box component="main" sx={{ flex: 1 }}>
        {children}
      </Box>
    </Box>
  );
}
