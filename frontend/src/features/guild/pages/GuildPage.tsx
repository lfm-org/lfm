import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";
import PageContainer from "../../../components/layout/PageContainer";
import SurfaceCard from "../../../components/SurfaceCard";
import api from "../../../lib/api";
import GuildSettingsEditor from "../components/GuildSettingsEditor";
import type { GuildHomeResponse } from "../lib/guildHome";
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
      setData(response.data);
      setSaveSuccess("Guild settings saved");
    } catch {
      setSaveError("Failed to save guild settings");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <PageContainer>
        <Stack direction="row" spacing={1.5} alignItems="center">
          <CircularProgress size={20} />
          <Typography color="text.secondary">Loading guild home...</Typography>
        </Stack>
      </PageContainer>
    );
  }

  return (
    <PageContainer>
      <Stack spacing={3}>
        <Box>
          <Typography component="h1" variant="h4" gutterBottom>
            Guild
          </Typography>
          <Typography color="text.secondary">
            Guild identity and setup status live here. Rank permissions and crest customization land in the next slices.
          </Typography>
        </Box>

        {error && <Alert severity="error">{error}</Alert>}

        {!error && !data?.guild && (
          <SurfaceCard>
            <Stack spacing={1.5}>
              <Typography variant="h6" component="h2">
                No guild on the active character
              </Typography>
              <Typography color="text.secondary">
                Select a character that belongs to a guild to unlock guild home, branding, and guild-only raid settings.
              </Typography>
            </Stack>
          </SurfaceCard>
        )}

        {!error && data?.guild && (
          <SurfaceCard sx={{ overflow: "hidden", borderRadius: 4 }}>
            <Box
              sx={{
                p: { xs: 3, md: 4 },
                background: "radial-gradient(circle at top left, rgba(100, 181, 246, 0.22), transparent 45%), linear-gradient(180deg, rgba(255,255,255,0.04), rgba(255,255,255,0))",
              }}
            >
              <Stack spacing={3}>
                <Stack direction={{ xs: "column", md: "row" }} spacing={3} alignItems={{ xs: "flex-start", md: "center" }}>
                  {data.guild.crestUrl ? (
                    <Box
                      component="img"
                      src={data.guild.crestUrl}
                      alt={`${data.guild.name} crest`}
                      sx={{
                        width: 92,
                        height: 92,
                        borderRadius: 3,
                        border: "1px solid rgba(255,255,255,0.12)",
                        background: "rgba(255,255,255,0.04)",
                        objectFit: "cover",
                      }}
                    />
                  ) : (
                    <Box
                      aria-hidden="true"
                      sx={{
                        width: 92,
                        height: 92,
                        borderRadius: 3,
                        display: "grid",
                        placeItems: "center",
                        border: "1px solid rgba(255,255,255,0.12)",
                        background: "linear-gradient(135deg, rgba(255,255,255,0.12), rgba(100,181,246,0.22))",
                        fontSize: "2rem",
                        fontWeight: 700,
                      }}
                    >
                      {data.guild.name.slice(0, 1).toUpperCase()}
                    </Box>
                  )}

                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: "0.18em" }}>
                      Realm Guild
                    </Typography>
                    <Typography variant="h3" component="h2" sx={{ lineHeight: 1.1 }}>
                      {data.guild.name}
                    </Typography>
                    <Typography color="text.secondary" sx={{ mt: 1 }}>
                      {data.guild.realmName}
                      {data.guild.factionName ? ` · ${data.guild.factionName}` : ""}
                    </Typography>
                  </Box>
                </Stack>

                <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} flexWrap="wrap" useFlexGap>
                  <Chip label={data.setup.isInitialized ? "Settings initialized" : "Settings pending"} color={data.setup.isInitialized ? "success" : "warning"} />
                  <Chip label={data.editor.canEdit ? "Editor access" : "Read-only member view"} variant="outlined" />
                  <Chip label={data.setup.rankDataFresh ? "Rank sync fresh" : "Rank sync not configured yet"} variant="outlined" />
                  {data.guild.memberCount != null && <Chip label={`${data.guild.memberCount} members`} variant="outlined" />}
                  {data.guild.syncedMemberCount != null && <Chip label={`${data.guild.syncedMemberCount} synced roster`} variant="outlined" />}
                  {data.guild.rankCount != null && <Chip label={`${data.guild.rankCount} ranks detected`} variant="outlined" />}
                  {data.guild.achievementPoints != null && <Chip label={`${data.guild.achievementPoints} achievement points`} variant="outlined" />}
                </Stack>

                <Typography color="text.secondary" sx={{ maxWidth: 760 }}>
                  {data.editor.canEdit
                    ? "Guild setup starts here. Choose the guild time zone and control which synced ranks can create or sign up to guild-only raids."
                    : "This guild home is intentionally read-only for now. The next slices add crest mirroring and rank-backed permissions without changing this route."}
                </Typography>

                {data.editor.canEdit ? (
                  <Stack spacing={2.5} sx={{ maxWidth: 640 }}>
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
                      rankPermissions={draft.rankPermissions}
                      saving={saving}
                      rankDataFresh={data.setup.rankDataFresh}
                      onTimezoneChange={(timezone) =>
                        setDraft((current) => ({ ...current, timezone }))
                      }
                      onPermissionChange={handlePermissionChange}
                      onSave={handleSaveSettings}
                    />
                  </Stack>
                ) : (
                  <Box>
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
            </Box>
          </SurfaceCard>
        )}
      </Stack>
    </PageContainer>
  );
}
