import { Box, Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";
import SurfaceCard from "../../../components/SurfaceCard";
import type { GuildHomeResponse } from "../lib/guildHome";

type GuildIdentity = NonNullable<GuildHomeResponse["guild"]>;

interface GuildIdentityCardProps {
  guild: GuildIdentity;
  metadata?: ReactNode;
}

export default function GuildIdentityCard({ guild, metadata }: GuildIdentityCardProps) {
  return (
    <SurfaceCard padding={3} sx={{ overflow: "hidden" }}>
      <Stack spacing={2.5}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2.5}
          alignItems={{ xs: "flex-start", sm: "center" }}
        >
          {guild.crestUrl ? (
            <Box
              component="img"
              src={guild.crestUrl}
              alt={`${guild.name} crest`}
              sx={{
                width: 88,
                height: 88,
                borderRadius: 2,
                border: "1px solid",
                borderColor: "divider",
                backgroundColor: "action.hover",
                objectFit: "cover",
                flexShrink: 0,
              }}
            />
          ) : (
            <Box
              aria-hidden="true"
              sx={{
                width: 88,
                height: 88,
                borderRadius: 2,
                display: "grid",
                placeItems: "center",
                border: "1px solid",
                borderColor: "divider",
                backgroundColor: "action.hover",
                fontSize: "2rem",
                fontWeight: 700,
                color: "text.primary",
                flexShrink: 0,
              }}
            >
              {guild.name.slice(0, 1).toUpperCase()}
            </Box>
          )}

          <Box sx={{ minWidth: 0 }}>
            <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: "0.18em" }}>
              Guild identity
            </Typography>
            <Typography variant="h4" component="h2" sx={{ lineHeight: 1.1 }}>
              {guild.name}
            </Typography>
            {guild.slogan && (
              <Typography color="text.secondary" sx={{ mt: 1 }}>
                {guild.slogan}
              </Typography>
            )}
            <Typography color="text.secondary" sx={{ mt: guild.slogan ? 1 : 0.5 }}>
              {guild.realmName}
              {guild.factionName ? ` · ${guild.factionName}` : ""}
            </Typography>
          </Box>
        </Stack>

        {metadata && (
          <Box sx={{ pt: 2, borderTop: "1px solid", borderColor: "divider" }}>
            <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: "0.18em" }}>
              Shared metadata
            </Typography>
            <Box sx={{ mt: 1.5 }}>{metadata}</Box>
          </Box>
        )}
      </Stack>
    </SurfaceCard>
  );
}
