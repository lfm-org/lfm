import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { Box, Button, Stack, Typography } from "@mui/material";
import api from "../../../lib/api";
import { useAuth } from "../../auth";

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

  if (loading) {
    return (
      <Box sx={{ maxWidth: 960, mx: "auto", px: 2, py: 4 }}>
        <Typography>Loading characters...</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ maxWidth: 960, mx: "auto", px: 2, py: 4 }}>
      <Stack spacing={3}>
        <Box>
          <Typography component="h1" variant="h5" gutterBottom>
            Select your character
          </Typography>
          {characters.length === 0 && (
            <Typography color="text.secondary">
              No Battle.net characters found.
            </Typography>
          )}
        </Box>

        <Box
          sx={{
            display: "grid",
            gap: 2,
            gridTemplateColumns: {
              xs: "1fr",
              sm: "repeat(auto-fit, minmax(180px, 1fr))",
            },
          }}
        >
          {characters.map((char) => (
            <Button
              key={`${char.realm}-${char.name}`}
              variant="outlined"
              onClick={() => selectCharacter(char)}
              sx={{
                p: 2,
                minHeight: 120,
                display: "flex",
                flexDirection: "column",
                alignItems: "flex-start",
                justifyContent: "flex-start",
                gap: 0.5,
                textAlign: "left",
                bgcolor: "background.paper",
              }}
            >
              <Typography variant="body1" component="span">{char.name}</Typography>
              <Typography variant="caption" color="text.secondary">{char.realmName}</Typography>
              <Typography variant="caption" color="text.secondary">Level {char.level}</Typography>
            </Button>
          ))}
        </Box>
      </Stack>
    </Box>
  );
}
