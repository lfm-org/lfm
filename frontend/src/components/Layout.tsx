import { useEffect, useState, type ReactNode } from "react";
import NavBar from "./NavBar";
import { checkAuth } from "../lib/auth";
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
  const [character, setCharacter] = useState<CharacterData | null>(null);

  useEffect(() => {
    checkAuth().then(user => {
      if (user?.selectedCharacterId) {
        api.get<{ characters: CharacterInfo[]; selectedCharacterId: string }>("/raider/characters")
          .then(res => {
            const selected = res.data.characters.find(
              (c: CharacterInfo) => c.id === res.data.selectedCharacterId
            );
            if (selected) setCharacter({ name: selected.name, portraitUrl: selected.portraitUrl });
          })
          .catch(() => {});
      }
    });
  }, []);

  return (
    <>
      <NavBar character={character} />
      {children}
    </>
  );
}
