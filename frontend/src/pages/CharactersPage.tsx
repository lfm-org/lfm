import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { Typography, Button } from "@mui/material";
import api from "../lib/api";
import { useAuth } from "../lib/AuthContext";

interface AccountCharacter {
  name: string;
  realm: string;
  realmName: string;
  level: number;
  region: string;
}

export default function CharactersPage() {
  const [characters, setCharacters] = useState<AccountCharacter[]>([]);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { onCharacterSelected } = useAuth();
  const redirectPath = (() => {
    const requested = searchParams.get("redirect");
    return requested && requested.startsWith("/") ? requested : "/raids";
  })();

  useEffect(() => {
    api.get<AccountCharacter[]>("/battlenet/characters").then(res => {
      const sorted = [...res.data].sort((a, b) => b.level - a.level);
      setCharacters(sorted);
      setLoading(false);
    }).catch(() => setLoading(false));
  }, []);

  const selectCharacter = async (char: AccountCharacter) => {
    const res = await api.post<{ selectedCharacterId: string }>("/raider/character", {
      region: char.region,
      realm: char.realm,
      name: char.name,
    });
    onCharacterSelected(res.data.selectedCharacterId);
    navigate(redirectPath);
  };

  if (loading) return <Typography style={{ padding: "2rem" }}>Loading characters...</Typography>;

  return (
    <div style={{ padding: "2rem" }}>
      <Typography variant="h5" gutterBottom>
        Select your character
      </Typography>
      {characters.length === 0 && (
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          No Battle.net characters found.
        </Typography>
      )}
      <div style={{ display: "flex", flexWrap: "wrap", gap: "1rem" }}>
        {characters.map((char) => (
          <Button
            key={`${char.realm}-${char.name}`}
            variant="outlined"
            onClick={() => selectCharacter(char)}
            style={{ display: "flex", flexDirection: "column", padding: "1rem", minWidth: "120px" }}
          >
            <Typography variant="body1" component="span">{char.name}</Typography>
            <Typography variant="caption">{char.realmName}</Typography>
            <Typography variant="caption">Level {char.level}</Typography>
          </Button>
        ))}
      </div>
    </div>
  );
}
