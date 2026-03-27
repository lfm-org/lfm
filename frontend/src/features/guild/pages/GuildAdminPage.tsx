import { useState } from "react";
import { Alert, Box, Button, Chip, CircularProgress, Stack, TextField, Typography } from "@mui/material";
import SurfaceCard from "../../../components/SurfaceCard";
import api from "../../../lib/api";
import { useAuth } from "../../auth";
import GuildIdentityCard from "../components/GuildIdentityCard";
import GuildRouteShell from "../components/GuildRouteShell";
import GuildSettingsEditor from "../components/GuildSettingsEditor";
import type { GuildHomeResponse } from "../lib/guildHome";
import {
  createGuildSettingsDraft,
  toGuildSettingsPayload,
  updateGuildRankPermission,
} from "../lib/guildSettingsForm";

interface ResolveResponse {
  guildId: string;
  guildName: string | null;
}

export default function GuildAdminPage() {
  const { user } = useAuth();
  const [guildId, setGuildId] = useState("");
  const [resolving, setResolving] = useState(false);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [resolved, setResolved] = useState<ResolveResponse | null>(null);
  const [data, setData] = useState<GuildHomeResponse | null>(null);
  const [draft, setDraft] = useState(() => createGuildSettingsDraft(data));

  const handlePermissionChange = (
    rank: number,
    field: "canCreateGuildRaids" | "canSignupGuildRaids",
    checked: boolean,
  ) => {
    setDraft((current) => ({
      ...current,
      rankPermissions: updateGuildRankPermission(current.rankPermissions, rank, field, checked),
    }));
  };

  const loadGuild = async (targetGuildId: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await api.get<GuildHomeResponse>(`/guild/admin/${targetGuildId}`);
      setData(response.data);
      setDraft(createGuildSettingsDraft(response.data));
    } catch {
      setError("Failed to load guild settings");
      setData(null);
    } finally {
      setLoading(false);
    }
  };

  const handleResolve = async () => {
    setResolving(true);
    setError(null);
    setSuccess(null);
    try {
      const response = await api.post<ResolveResponse>("/guild/admin/resolve", { guildId });
      setResolved(response.data);
      await loadGuild(response.data.guildId);
    } catch {
      setResolved(null);
      setData(null);
      setError("Failed to resolve guild");
    } finally {
      setResolving(false);
    }
  };

  const handleSave = async () => {
    if (!resolved) return;
    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      const response = await api.put<GuildHomeResponse>(
        `/guild/admin/${resolved.guildId}/settings`,
        toGuildSettingsPayload(draft),
      );
      setData(response.data);
      setDraft(createGuildSettingsDraft(response.data));
      setSuccess("Guild settings saved");
    } catch {
      setError("Failed to save guild settings");
    } finally {
      setSaving(false);
    }
  };

  if (!user?.isSiteAdmin) {
    return (
      <GuildRouteShell
        title="Guild admin"
        description="Resolve a guild explicitly, then edit the same settings through the site-admin override path."
      >
        <Stack spacing={3}>
          <Alert severity="error">Site admin access required.</Alert>
        </Stack>
      </GuildRouteShell>
    );
  }

  return (
    <GuildRouteShell
      title="Guild admin"
      description="Resolve a guild explicitly, then edit the same settings through the site-admin override path."
    >
      <Stack spacing={3}>
        {error && <Alert severity="error">{error}</Alert>}
        {success && <Alert severity="success">{success}</Alert>}

        <SurfaceCard padding={3}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} alignItems={{ xs: "stretch", sm: "flex-end" }}>
            <TextField
              label="Guild ID"
              value={guildId}
              onChange={(event) => setGuildId(event.target.value)}
              sx={{ maxWidth: 240 }}
            />
            <Button variant="contained" onClick={handleResolve} disabled={resolving || !guildId.trim()}>
              {resolving ? "Loading..." : "Load guild"}
            </Button>
          </Stack>
        </SurfaceCard>

        {loading && (
          <Stack direction="row" spacing={1.5} alignItems="center">
            <CircularProgress size={20} />
          <Typography color="text.secondary">Loading guild override data...</Typography>
        </Stack>
      )}

      {resolved && data?.guild && (
        <Stack spacing={3}>
          <GuildIdentityCard
            guild={data.guild}
            metadata={(
              <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} flexWrap="wrap" useFlexGap>
                <Chip label={`Guild ID ${resolved.guildId}`} variant="outlined" />
                <Chip
                  label={data.setup.rankDataFresh ? "Rank sync fresh" : "Rank sync not configured yet"}
                  variant="outlined"
                />
                {data.guild.memberCount != null && (
                  <Chip label={`${data.guild.memberCount} members`} variant="outlined" />
                )}
                {data.guild.syncedMemberCount != null && (
                  <Chip label={`${data.guild.syncedMemberCount} synced roster`} variant="outlined" />
                )}
                {data.guild.rankCount != null && (
                  <Chip label={`${data.guild.rankCount} ranks detected`} variant="outlined" />
                )}
                {data.guild.achievementPoints != null && (
                  <Chip label={`${data.guild.achievementPoints} achievement points`} variant="outlined" />
                )}
              </Stack>
            )}
          />

          <SurfaceCard padding={3}>
            <Stack spacing={2.5}>
              <Box>
                <Typography variant="h6" component="h2">
                  Override settings
                </Typography>
                <Typography color="text.secondary">
                  Editing {resolved.guildName ?? data.guild.name}
                </Typography>
              </Box>

              {!data.setup.rankDataFresh && (
                <Alert severity="error">Rank sync is stale. Guild settings are locked until roster data refreshes.</Alert>
              )}

              {data.adminOverride?.lastOverrideAt && (
                <Alert severity="info">
                  Last override by {data.adminOverride.lastOverrideBy ?? "unknown"} at {data.adminOverride.lastOverrideAt}
                </Alert>
              )}

              <GuildSettingsEditor
                timezone={draft.timezone}
                slogan={draft.slogan}
                rankPermissions={draft.rankPermissions}
                saving={saving}
                rankDataFresh={data.setup.rankDataFresh}
                onTimezoneChange={(timezone) =>
                  setDraft((current) => ({ ...current, timezone }))
                }
                onSloganChange={(slogan) => setDraft((current) => ({ ...current, slogan }))}
                onPermissionChange={handlePermissionChange}
                onSave={handleSave}
              />
            </Stack>
          </SurfaceCard>
        </Stack>
      )}
    </Stack>
  </GuildRouteShell>
  );
}
