import {
  Box,
  Button,
  Checkbox,
  FormControl,
  FormControlLabel,
  InputLabel,
  NativeSelect,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { GUILD_LOCALE_OPTIONS, GUILD_TIMEZONE_OPTIONS } from "../../../lib/guildConfig";
import type { GuildRankPermission } from "../lib/guildSettingsForm";

interface GuildSettingsEditorProps {
  timezone: string;
  locale: string;
  slogan: string;
  rankPermissions: GuildRankPermission[];
  saving: boolean;
  rankDataFresh: boolean;
  onTimezoneChange: (value: string) => void;
  onLocaleChange: (value: string) => void;
  onSloganChange: (value: string) => void;
  onPermissionChange: (
    rank: number,
    field: "canCreateGuildRaids" | "canSignupGuildRaids" | "canDeleteGuildRaids",
    checked: boolean,
  ) => void;
  onSave: () => void;
}

export default function GuildSettingsEditor(props: GuildSettingsEditorProps) {
  const { t } = useTranslation();
  return (
    <Stack spacing={2.5} sx={{ maxWidth: 640 }}>
      <FormControl fullWidth sx={{ maxWidth: 320 }}>
        <InputLabel htmlFor="guild-timezone">{t("guildSettings.timezone")}</InputLabel>
        <NativeSelect
          value={props.timezone}
          onChange={(event) => props.onTimezoneChange(event.target.value)}
          inputProps={{ id: "guild-timezone", name: "timezone" }}
          disabled={!props.rankDataFresh || props.saving}
        >
          {GUILD_TIMEZONE_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </NativeSelect>
      </FormControl>

      <FormControl fullWidth sx={{ maxWidth: 320 }}>
        <InputLabel htmlFor="guild-locale">{t("guildSettings.locale")}</InputLabel>
        <NativeSelect
          value={props.locale}
          onChange={(event) => props.onLocaleChange(event.target.value)}
          inputProps={{ id: "guild-locale", name: "locale" }}
          disabled={!props.rankDataFresh || props.saving}
        >
          {GUILD_LOCALE_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </NativeSelect>
      </FormControl>

      <TextField
        label={t("guildSettings.slogan")}
        value={props.slogan}
        onChange={(event) => props.onSloganChange(event.target.value)}
        disabled={!props.rankDataFresh || props.saving}
        multiline
        minRows={2}
        helperText={t("guildSettings.sloganHelper")}
        sx={{ maxWidth: 480 }}
      />

      {props.rankPermissions.length > 0 && (
        <Stack spacing={1.5}>
          <Typography variant="h6" component="h3">
            {t("guildSettings.rankPermissions")}
          </Typography>
          {props.rankPermissions.map((permission) => (
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
              <Typography fontWeight={600}>{t("guildSettings.rank", { rank: permission.rank })}</Typography>
              <FormControlLabel
                control={(
                  <Checkbox
                    checked={permission.canCreateGuildRaids}
                    onChange={(event) =>
                      props.onPermissionChange(
                        permission.rank,
                        "canCreateGuildRaids",
                        event.target.checked,
                      )}
                    disabled={!props.rankDataFresh || props.saving}
                  />
                )}
                label={t("guildSettings.allowCreate", { rank: permission.rank })}
              />
              <FormControlLabel
                control={(
                  <Checkbox
                    checked={permission.canSignupGuildRaids}
                    onChange={(event) =>
                      props.onPermissionChange(
                        permission.rank,
                        "canSignupGuildRaids",
                        event.target.checked,
                      )}
                    disabled={!props.rankDataFresh || props.saving}
                  />
                )}
                label={t("guildSettings.allowSignup", { rank: permission.rank })}
              />
              <FormControlLabel
                control={(
                  <Checkbox
                    checked={permission.canDeleteGuildRaids}
                    onChange={(event) =>
                      props.onPermissionChange(
                        permission.rank,
                        "canDeleteGuildRaids",
                        event.target.checked,
                      )}
                    disabled={!props.rankDataFresh || props.saving}
                  />
                )}
                label={t("guildSettings.allowDelete", { rank: permission.rank })}
              />
            </Box>
          ))}
        </Stack>
      )}

      <Box>
        <Button
          variant="contained"
          onClick={props.onSave}
          disabled={props.saving || !props.rankDataFresh}
        >
          {props.saving ? t("guildSettings.saving") : t("guildSettings.saveButton")}
        </Button>
      </Box>
    </Stack>
  );
}
