import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { Typography, Button } from "@mui/material";
import api from "../lib/api";
import { useAuth } from "../lib/AuthContext";

interface AccountCharacter {
  name: string;
  realm: { slug: string; name: string | Record<string, string> };
  level: number;
  playable_class: { name: string };
  playable_race: { name: string };
}

function realmDisplayName(name: string | Record<string, string>): string {
  if (typeof name === "string") return name;
  return name.en_GB ?? name.en_US ?? Object.values(name)[0] ?? "";
}

interface WowAccount {
  characters: AccountCharacter[];
}

interface ProfileData {
  wow_accounts: WowAccount[];
}

export default function CharactersPage() {
  const [characters, setCharacters] = useState<AccountCharacter[]>([]);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();
  const { refresh } = useAuth();

  useEffect(() => {
    api.get<ProfileData>("/battlenet/characters").then(res => {
      const allChars = res.data.wow_accounts?.flatMap(a => a.characters) ?? [];
      // Sort by level descending
      allChars.sort((a, b) => b.level - a.level);
      setCharacters(allChars);
      setLoading(false);
    }).catch(() => setLoading(false));
  }, []);

  const selectCharacter = async (char: AccountCharacter) => {
    const region = import.meta.env.VITE_BATTLE_NET_REGION || "eu";
    await api.post("/raider/character", {
      region,
      realm: char.realm.slug,
      name: char.name,
    });
    await refresh();
    navigate("/raids");
  };

  if (loading) return <Typography style={{ padding: "2rem" }}>Loading characters...</Typography>;

  return (
    <div style={{ padding: "2rem" }}>
      <Typography variant="h5" gutterBottom>
        Select your character
      </Typography>
      <div style={{ display: "flex", flexWrap: "wrap", gap: "1rem" }}>
        {characters.map((char) => (
          <Button
            key={`${char.realm.slug}-${char.name}`}
            variant="outlined"
            onClick={() => selectCharacter(char)}
            style={{ display: "flex", flexDirection: "column", padding: "1rem", minWidth: "120px" }}
          >
            <Typography variant="body1" component="span">{char.name}</Typography>
            <Typography variant="caption">{realmDisplayName(char.realm.name)}</Typography>
            <Typography variant="caption">Level {char.level}</Typography>
          </Button>
        ))}
      </div>
    </div>
  );
}
