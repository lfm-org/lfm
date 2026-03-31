import { useEffect, useState } from "react";
import { Alert, Box, Chip, CircularProgress, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
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
  const { t } = useTranslation();
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
      setSaveSuccess(t("guild.settingsSaved"));
    } catch (error) {
      setSaveError(getApiErrorMessage(error, t("guild.settingsSaveFailed")));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <GuildRouteShell
        title={t("guild.title")}
        description={t("guild.description")}
      >
        <Stack direction="row" spacing={1.5} alignItems="center">
          <CircularProgress size={20} />
          <Typography color="text.secondary">{t("guild.loading")}</Typography>
        </Stack>
      </GuildRouteShell>
    );
  }

  return (
    <GuildRouteShell
      title={t("guild.title")}
      description={t("guild.description")}
    >
      <Stack spacing={3}>
        {error && <Alert severity="error">{error}</Alert>}

        {!error && !data?.guild && (
          <SurfaceCard padding={3}>
            <Stack spacing={1.5}>
              <Typography variant="h6" component="h2">
                {t("guild.noGuild.title")}
              </Typography>
              <Typography color="text.secondary">
                {t("guild.noGuild.body")}
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
                    label={data.setup.isInitialized ? t("guild.chip.settingsLive") : t("guild.chip.settingsPending")}
                    color={data.setup.isInitialized ? "success" : "warning"}
                  />
                  <Chip
                    label={data.editor.canEdit ? t("guild.chip.editable") : t("guild.chip.readOnly")}
                    variant="outlined"
                  />
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
              <Stack spacing={3}>
                {data.editor.canEdit ? (
                  <Stack spacing={2.5}>
                    {data.setup.requiresSetup && (
                      <Alert severity="warning">{t("guild.setupRequired")}</Alert>
                    )}
                    {!data.setup.rankDataFresh && (
                      <Alert severity="error">{t("guild.rankSyncStale")}</Alert>
                    )}
                    {saveError && <Alert severity="error">{saveError}</Alert>}
                    {saveSuccess && <Alert severity="success">{saveSuccess}</Alert>}
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
                      onSave={handleSaveSettings}
                    />
                  </Stack>
                ) : (
                  <Box>
                    <Typography variant="h6" component="h2" gutterBottom>
                      {t("guild.memberAccess")}
                    </Typography>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} flexWrap="wrap" useFlexGap>
                      <Chip
                        label={data.memberPermissions.canCreateGuildRaids ? t("guild.chip.canCreate") : t("guild.chip.cannotCreate")}
                        color={data.memberPermissions.canCreateGuildRaids ? "success" : "default"}
                        variant={data.memberPermissions.canCreateGuildRaids ? "filled" : "outlined"}
                      />
                      <Chip
                        label={data.memberPermissions.canSignupGuildRaids ? t("guild.chip.canSignup") : t("guild.chip.cannotSignup")}
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
