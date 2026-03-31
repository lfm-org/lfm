import { useState } from "react";
import { Alert, Box, Button, Chip, CircularProgress, Stack, TextField, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import SurfaceCard from "../../../components/SurfaceCard";
import api, { getApiErrorMessage } from "../../../lib/api";
import { useAuth } from "../../auth";
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

interface ResolveResponse {
  guildId: string;
  guildName: string | null;
}

export default function GuildAdminPage() {
  const { t } = useTranslation();
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
      const normalized = normalizeGuildHomeResponse(response.data);
      setData(normalized);
      setDraft(createGuildSettingsDraft(normalized));
    } catch {
      setError(t("guildAdmin.loadFailed"));
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
      setError(t("guildAdmin.resolveFailed"));
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
      const normalized = normalizeGuildHomeResponse(response.data);
      setData(normalized);
      setDraft(createGuildSettingsDraft(normalized));
      setSuccess(t("guild.settingsSaved"));
    } catch (error) {
      setError(getApiErrorMessage(error, t("guild.settingsSaveFailed")));
    } finally {
      setSaving(false);
    }
  };

  if (!user?.isSiteAdmin) {
    return (
      <GuildRouteShell
        title={t("guildAdmin.title")}
        description={t("guildAdmin.description")}
      >
        <Stack spacing={3}>
          <Alert severity="error">{t("guildAdmin.accessRequired")}</Alert>
        </Stack>
      </GuildRouteShell>
    );
  }

  return (
    <GuildRouteShell
      title={t("guildAdmin.title")}
      description={t("guildAdmin.description")}
    >
      <Stack spacing={3}>
        {error && <Alert severity="error">{error}</Alert>}
        {success && <Alert severity="success">{success}</Alert>}

        <SurfaceCard padding={3}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} alignItems={{ xs: "stretch", sm: "flex-end" }}>
            <TextField
              label={t("guildAdmin.guildIdLabel")}
              value={guildId}
              onChange={(event) => setGuildId(event.target.value)}
              sx={{ maxWidth: 240 }}
            />
            <Button variant="contained" onClick={handleResolve} disabled={resolving || !guildId.trim()}>
              {resolving ? t("guildAdmin.loadButtonLoading") : t("guildAdmin.loadButton")}
            </Button>
          </Stack>
        </SurfaceCard>

        {loading && (
          <Stack direction="row" spacing={1.5} alignItems="center">
            <CircularProgress size={20} />
          <Typography color="text.secondary">{t("guildAdmin.loading")}</Typography>
        </Stack>
      )}

      {resolved && data?.guild && (
        <Stack spacing={3}>
          <GuildIdentityCard
            guild={data.guild}
            metadata={(
              <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} flexWrap="wrap" useFlexGap>
                <Chip label={t("guildAdmin.guildIdChip", { id: resolved.guildId })} variant="outlined" />
                <Chip
                  label={data.setup.rankDataFresh ? t("guild.chip.rankSyncFresh") : t("guild.chip.rankSyncNotConfigured")}
                  variant="outlined"
                />
                {data.guild.memberCount != null && (
                  <Chip label={t("guild.chip.members", { count: data.guild.memberCount })} variant="outlined" />
                )}
                {data.guild.syncedMemberCount != null && (
                  <Chip label={t("guild.chip.syncedRoster", { count: data.guild.syncedMemberCount })} variant="outlined" />
                )}
                {data.guild.rankCount != null && (
                  <Chip label={t("guild.chip.ranksDetected", { count: data.guild.rankCount })} variant="outlined" />
                )}
                {data.guild.achievementPoints != null && (
                  <Chip label={t("guild.chip.achievementPoints", { count: data.guild.achievementPoints })} variant="outlined" />
                )}
              </Stack>
            )}
          />

          <SurfaceCard padding={3}>
            <Stack spacing={2.5}>
              <Box>
                <Typography variant="h6" component="h2">
                  {t("guildAdmin.overrideSettings")}
                </Typography>
                <Typography color="text.secondary">
                  {t("guildAdmin.editing", { name: resolved.guildName ?? data.guild.name })}
                </Typography>
              </Box>

              {!data.setup.rankDataFresh && (
                <Alert severity="error">{t("guild.rankSyncStale")}</Alert>
              )}

              {data.adminOverride?.lastOverrideAt && (
                <Alert severity="info">
                  {t("guildAdmin.lastOverride", { by: data.adminOverride.lastOverrideBy ?? "unknown", at: data.adminOverride.lastOverrideAt })}
                </Alert>
              )}

              <GuildSettingsEditor
                timezone={draft.timezone}
                locale={draft.locale}
                slogan={draft.slogan}
                rankPermissions={draft.rankPermissions}
                saving={saving}
                rankDataFresh={data.setup.rankDataFresh}
                onTimezoneChange={(timezone) =>
                  setDraft((current) => ({ ...current, timezone }))
                }
                onLocaleChange={(locale) =>
                  setDraft((current) => ({ ...current, locale }))
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
