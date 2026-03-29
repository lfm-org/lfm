import { useEffect, useState } from "react";
import { Alert, Box, Chip, CircularProgress, Stack, Typography } from "@mui/material";
import SurfaceCard from "../../../components/SurfaceCard";
import api, { getApiErrorMessage } from "../../../lib/api";
import GuildIdentityCard from "../components/GuildIdentityCard";
import GuildRouteShell from "../components/GuildRouteShell";
import GuildSettingsEditor from "../components/GuildSettingsEditor";
import type { GuildHomeResponse } from "../lib/guildHome";
import { normalizeGuildHomeResponse } from "../lib/guildHome";
import {
  createGuildSettingsDraft,
  toGuildSettingsPayload,
  updateGuildRankPermission,
} from "../lib/guildSettingsForm";
import { useGuildHome } from "../lib/useGuildHome";

export default function GuildPage() {
  const { data, loading, error, setData } = useGuildHome();
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [draft, setDraft] = useState(() => createGuildSettingsDraft(data ?? null));

  useEffect(() => {
    setDraft(createGuildSettingsDraft(data ?? null));
  }, [data]);

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

  const handleSaveSettings = async () => {
    setSaving(true);
    setSaveError(null);
    setSaveSuccess(null);

    try {
      const response = await api.put<GuildHomeResponse>(
        "/guild/settings",
        toGuildSettingsPayload(draft),
      );
      setData(normalizeGuildHomeResponse(response.data));
      setSaveSuccess("Guild settings saved");
    } catch (error) {
      setSaveError(getApiErrorMessage(error, "Failed to save guild settings"));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <GuildRouteShell
        title="Guild"
        description="Review the active guild, keep its slogan current, and manage rank-gated raid settings from one shared surface."
      >
        <Stack direction="row" spacing={1.5} alignItems="center">
          <CircularProgress size={20} />
          <Typography color="text.secondary">Loading guild home...</Typography>
        </Stack>
      </GuildRouteShell>
    );
  }

  return (
    <GuildRouteShell
      title="Guild"
      description="Review the active guild, keep its slogan current, and manage rank-gated raid settings from one shared surface."
    >
      <Stack spacing={3}>
        {error && <Alert severity="error">{error}</Alert>}

        {!error && !data?.guild && (
          <SurfaceCard padding={3}>
            <Stack spacing={1.5}>
              <Typography variant="h6" component="h2">
                No guild on the active character
              </Typography>
              <Typography color="text.secondary">
                Select a character that belongs to a guild to unlock the shared guild surface.
              </Typography>
            </Stack>
          </SurfaceCard>
        )}

        {!error && data?.guild && (
          <Stack spacing={3}>
            <GuildIdentityCard
              guild={data.guild}
              metadata={(
                <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} flexWrap="wrap" useFlexGap>
                  <Chip
                    label={data.setup.isInitialized ? "Settings live" : "Settings pending"}
                    color={data.setup.isInitialized ? "success" : "warning"}
                  />
                  <Chip
                    label={data.editor.canEdit ? "Editable" : "Read-only"}
                    variant="outlined"
                  />
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
              <Stack spacing={3}>
                {data.editor.canEdit ? (
                  <Stack spacing={2.5}>
                    {data.setup.requiresSetup && (
                      <Alert severity="warning">Guild master setup required</Alert>
                    )}
                    {!data.setup.rankDataFresh && (
                      <Alert severity="error">Rank sync is stale. Guild settings are locked until roster data refreshes.</Alert>
                    )}
                    {saveError && <Alert severity="error">{saveError}</Alert>}
                    {saveSuccess && <Alert severity="success">{saveSuccess}</Alert>}
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
                      onSave={handleSaveSettings}
                    />
                  </Stack>
                ) : (
                  <Box>
                    <Typography variant="h6" component="h2" gutterBottom>
                      Member access
                    </Typography>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} flexWrap="wrap" useFlexGap>
                      <Chip
                        label={data.memberPermissions.canCreateGuildRaids ? "You can create guild raids" : "Guild raid creation blocked for your rank"}
                        color={data.memberPermissions.canCreateGuildRaids ? "success" : "default"}
                        variant={data.memberPermissions.canCreateGuildRaids ? "filled" : "outlined"}
                      />
                      <Chip
                        label={data.memberPermissions.canSignupGuildRaids ? "You can sign up to guild raids" : "Guild signup blocked for your rank"}
                        color={data.memberPermissions.canSignupGuildRaids ? "success" : "default"}
                        variant={data.memberPermissions.canSignupGuildRaids ? "filled" : "outlined"}
                      />
                    </Stack>
                  </Box>
                )}
              </Stack>
            </SurfaceCard>
          </Stack>
        )}
      </Stack>
    </GuildRouteShell>
  );
}
