import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { battlenet } from "@/lib/battlenet";
import { Typography, Button } from "@mui/material";

interface Props {
  searchParams: Promise<{ redirect?: string }>;
}

export default async function CharactersPage({ searchParams }: Props) {
  const cookieStore = await cookies();
  const token = cookieStore.get("battlenet_token")?.value;
  if (!token) redirect("/login");

  const identity = await battlenet.resolveIdentity(token);
  if (!identity) redirect("/login");

  const characters = await battlenet.fetchAccountCharacters(token);
  if (characters.length === 0) redirect("/login");

  const params = await searchParams;
  const redirectParam = params.redirect ?? "/raids";

  return (
    <div style={{ padding: "2rem" }}>
      <Typography variant="h5" gutterBottom>
        Select your character
      </Typography>
      <div style={{ display: "flex", flexWrap: "wrap", gap: "1rem" }}>
        {characters.map((char) => (
          <form
            key={`${char.region}-${char.realm}-${char.name}`}
            action={`/api/raider/character?redirect=${encodeURIComponent(redirectParam)}`}
            method="POST"
          >
            <input type="hidden" name="region" value={char.region} />
            <input type="hidden" name="realm" value={char.realm} />
            <input type="hidden" name="name" value={char.name} />
            <Button
              type="submit"
              variant="outlined"
              style={{ display: "flex", flexDirection: "column", padding: "1rem", minWidth: "120px" }}
            >
              <Typography variant="body1">{char.name}</Typography>
              <Typography variant="caption">{char.realmName}</Typography>
              <Typography variant="caption">Level {char.level}</Typography>
            </Button>
          </form>
        ))}
      </div>
    </div>
  );
}
