import {
  Box,
  Button,
  Checkbox,
  FormControl,
  FormControlLabel,
  InputLabel,
  NativeSelect,
  Stack,
  Typography,
} from "@mui/material";
import { GUILD_TIMEZONE_OPTIONS } from "../../../lib/guildConfig";
import type { GuildRankPermission } from "../lib/guildSettingsForm";

interface GuildSettingsEditorProps {
  timezone: string;
  rankPermissions: GuildRankPermission[];
  saving: boolean;
  rankDataFresh: boolean;
  onTimezoneChange: (value: string) => void;
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
