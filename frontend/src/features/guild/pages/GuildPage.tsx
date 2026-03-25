import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  FormControl,
  FormControlLabel,
  InputLabel,
  NativeSelect,
  Stack,
  Typography,
} from "@mui/material";
import PageContainer from "../../../components/layout/PageContainer";
import SurfaceCard from "../../../components/SurfaceCard";
import api from "../../../lib/api";
import { GUILD_TIMEZONE_OPTIONS } from "../../../lib/guildConfig";
import type { GuildHomeResponse } from "../lib/guildHome";
import { useGuildHome } from "../lib/useGuildHome";

export default function GuildPage() {
  const { data, loading, error, setData } = useGuildHome();
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [timezone, setTimezone] = useState("Europe/Helsinki");
  const [rankPermissions, setRankPermissions] = useState<NonNullable<GuildHomeResponse["settings"]>["rankPermissions"]>([]);

  useEffect(() => {
    if (data?.setup.timezone) {
      setTimezone(data.setup.timezone);
    }
    setRankPermissions(data?.settings?.rankPermissions ?? []);
  }, [data?.settings?.rankPermissions, data?.setup.timezone]);

  const handlePermissionChange = (
    rank: number,
    field: "canCreateGuildRaids" | "canSignupGuildRaids",
    checked: boolean
  ) => {
    setRankPermissions((current) => current.map((permission) => (
      permission.rank === rank
        ? { ...permission, [field]: checked }
        : permission
    )));
  };

  const handleSaveSettings = async () => {
    setSaving(true);
    setSaveError(null);
    setSaveSuccess(null);

    try {
      const response = await api.put<GuildHomeResponse>("/guild/settings", { timezone, rankPermissions });
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
                    <FormControl fullWidth sx={{ maxWidth: 320 }}>
                      <InputLabel htmlFor="guild-timezone">Time zone</InputLabel>
                      <NativeSelect
                        value={timezone}
                        onChange={(event) => setTimezone(event.target.value)}
                        inputProps={{ id: "guild-timezone", name: "timezone" }}
                        disabled={!data.setup.rankDataFresh || saving}
                      >
                        {GUILD_TIMEZONE_OPTIONS.map((option) => (
                          <option key={option} value={option}>{option}</option>
                        ))}
                      </NativeSelect>
                    </FormControl>

                    {rankPermissions.length > 0 && (
                      <Stack spacing={1.5}>
                        <Typography variant="h6" component="h3">Rank permissions</Typography>
                        {rankPermissions.map((permission) => (
                          <Box
                            key={permission.rank}
                            sx={{
                              display: "grid",
                              gap: 1,
                              p: 2,
                              border: "1px solid",
                              borderColor: "divider",
                              borderRadius: 2,
                            }}
                          >
                            <Typography fontWeight={600}>Rank {permission.rank}</Typography>
                            <FormControlLabel
                              control={(
                                <Checkbox
                                  checked={permission.canCreateGuildRaids}
                                  onChange={(event) =>
                                    handlePermissionChange(permission.rank, "canCreateGuildRaids", event.target.checked)}
                                  disabled={!data.setup.rankDataFresh || saving}
                                />
                              )}
                              label={`Allow guild raid creation for Rank ${permission.rank}`}
                            />
                            <FormControlLabel
                              control={(
                                <Checkbox
                                  checked={permission.canSignupGuildRaids}
                                  onChange={(event) =>
                                    handlePermissionChange(permission.rank, "canSignupGuildRaids", event.target.checked)}
                                  disabled={!data.setup.rankDataFresh || saving}
                                />
                              )}
                              label={`Allow guild raid signup for Rank ${permission.rank}`}
                            />
                          </Box>
                        ))}
                      </Stack>
                    )}
                    <Box>
                      <Button variant="contained" onClick={handleSaveSettings} disabled={saving || !data.setup.rankDataFresh}>
                        {saving ? "Saving..." : "Save guild settings"}
                      </Button>
                    </Box>
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
