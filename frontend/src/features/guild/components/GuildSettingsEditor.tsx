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
    field: "canCreateGuildRaids" | "canSignupGuildRaids",
    checked: boolean,
  ) => void;
  onSave: () => void;
}

export default function GuildSettingsEditor(props: GuildSettingsEditorProps) {
  return (
    <Stack spacing={2.5} sx={{ maxWidth: 640 }}>
      <FormControl fullWidth sx={{ maxWidth: 320 }}>
        <InputLabel htmlFor="guild-timezone">Time zone</InputLabel>
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
        <InputLabel htmlFor="guild-locale">Locale</InputLabel>
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
        label="Slogan"
        value={props.slogan}
        onChange={(event) => props.onSloganChange(event.target.value)}
        disabled={!props.rankDataFresh || props.saving}
        multiline
        minRows={2}
        helperText="Shown beside the guild name."
        sx={{ maxWidth: 480 }}
      />

      {props.rankPermissions.length > 0 && (
        <Stack spacing={1.5}>
          <Typography variant="h6" component="h3">
            Rank permissions
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
              <Typography fontWeight={600}>Rank {permission.rank}</Typography>
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
                label={`Allow guild raid creation for Rank ${permission.rank}`}
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
                label={`Allow guild raid signup for Rank ${permission.rank}`}
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
          {props.saving ? "Saving..." : "Save guild settings"}
        </Button>
      </Box>
    </Stack>
  );
}
