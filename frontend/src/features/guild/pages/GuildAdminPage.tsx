import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  FormControl,
  FormControlLabel,
  InputLabel,
  NativeSelect,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import PageContainer from "../../../components/layout/PageContainer";
import SurfaceCard from "../../../components/SurfaceCard";
import api from "../../../lib/api";
import { useAuth } from "../../auth";
import type { GuildHomeResponse } from "../lib/guildHome";
import { GUILD_TIMEZONE_OPTIONS } from "../../../lib/guildConfig";

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
  const [timezone, setTimezone] = useState("Europe/Helsinki");
  const [rankPermissions, setRankPermissions] = useState<NonNullable<GuildHomeResponse["settings"]>["rankPermissions"]>([]);

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

  const loadGuild = async (targetGuildId: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await api.get<GuildHomeResponse>(`/guild/admin/${targetGuildId}`);
      setData(response.data);
      setTimezone(response.data.setup.timezone);
      setRankPermissions(response.data.settings?.rankPermissions ?? []);
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
      const response = await api.put<GuildHomeResponse>(`/guild/admin/${resolved.guildId}/settings`, {
        timezone,
        rankPermissions,
      });
      setData(response.data);
      setTimezone(response.data.setup.timezone);
      setRankPermissions(response.data.settings?.rankPermissions ?? []);
      setSuccess("Guild settings saved");
    } catch {
      setError("Failed to save guild settings");
    } finally {
      setSaving(false);
    }
  };

  if (!user?.isSiteAdmin) {
    return (
      <PageContainer>
        <Stack spacing={3}>
          <Box>
            <Typography component="h1" variant="h4" gutterBottom>
              Guild Admin
            </Typography>
            <Typography color="text.secondary">
              Explicit guild override is restricted to the configured site-admin allowlist.
            </Typography>
          </Box>
          <Alert severity="error">Site admin access required.</Alert>
        </Stack>
      </PageContainer>
    );
  }

  return (
    <PageContainer>
      <Stack spacing={3}>
        <Box>
          <Typography component="h1" variant="h4" gutterBottom>
            Guild Admin
          </Typography>
          <Typography color="text.secondary">
            Resolve a guild explicitly, then edit its settings through the override path. This does not change normal guild-master permissions.
          </Typography>
        </Box>

        {error && <Alert severity="error">{error}</Alert>}
        {success && <Alert severity="success">{success}</Alert>}

        <SurfaceCard>
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
          <SurfaceCard sx={{ overflow: "hidden", borderRadius: 4 }}>
            <Box sx={{ p: { xs: 3, md: 4 } }}>
              <Stack spacing={3}>
                <Box>
                  <Typography variant="h5" component="h2">
                    Editing {resolved.guildName ?? data.guild.name}
                  </Typography>
                  <Typography color="text.secondary">
                    Guild ID {resolved.guildId}
                  </Typography>
                </Box>

                {data.adminOverride?.lastOverrideAt && (
                  <Alert severity="info">
                    Last override by {data.adminOverride.lastOverrideBy ?? "unknown"} at {data.adminOverride.lastOverrideAt}
                  </Alert>
                )}

                <FormControl fullWidth sx={{ maxWidth: 320 }}>
                  <InputLabel htmlFor="guild-admin-timezone">Time zone</InputLabel>
                  <NativeSelect
                    value={timezone}
                    onChange={(event) => setTimezone(event.target.value)}
                    inputProps={{ id: "guild-admin-timezone", name: "timezone" }}
                    disabled={saving}
                  >
                    {GUILD_TIMEZONE_OPTIONS.map((option) => (
                      <option key={option} value={option}>{option}</option>
                    ))}
                  </NativeSelect>
                </FormControl>

                <Stack spacing={1.5}>
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
                            disabled={saving}
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
                            disabled={saving}
                          />
                        )}
                        label={`Allow guild raid signup for Rank ${permission.rank}`}
                      />
                    </Box>
                  ))}
                </Stack>

                <Box>
                  <Button variant="contained" onClick={handleSave} disabled={saving}>
                    {saving ? "Saving..." : "Save guild settings"}
                  </Button>
                </Box>
              </Stack>
            </Box>
          </SurfaceCard>
        )}
      </Stack>
    </PageContainer>
  );
}
