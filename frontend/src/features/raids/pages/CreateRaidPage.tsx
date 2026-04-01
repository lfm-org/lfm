import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import {
  Box, Typography, TextField, Button, Alert, CircularProgress,
  FormControl, InputLabel, Select, MenuItem,
  ToggleButtonGroup, ToggleButton,
} from "@mui/material";
import FormHelperText from "@mui/material/FormHelperText";
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { AdapterLuxon } from "@mui/x-date-pickers/AdapterLuxon";
import DOMPurify from "dompurify";
import { DateTime } from "luxon";
import api from "../../../lib/api";
import {
  formatInstanceModeLabel,
  normalizeWowInstances,
  toModeKey,
  type WowInstance,
} from "../../../lib/wow/instances";
import PageContainer from "../../../components/layout/PageContainer";
import DateTimeInput from "../../../components/DateTimeInput";
import { useGuildHome } from "../../guild/lib/useGuildHome";
import { validateRaidForm, type FormField } from "../lib/raidValidation";

const MAX_DESCRIPTION = 500;

export default function CreateRaidPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { data: guildHome } = useGuildHome();

  const [instances, setInstances] = useState<WowInstance[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [instanceId, setInstanceId] = useState<number | "">("");
  const [startTime, setStartTime] = useState<DateTime | null>(null);
  const [signupCloseTime, setSignupCloseTime] = useState<DateTime | null>(null);
  const [description, setDescription] = useState("");
  const [selectedModeKey, setSelectedModeKey] = useState("");
  const [visibility, setVisibility] = useState<"GUILD" | "PUBLIC">("PUBLIC");
  const [errors, setErrors] = useState<Partial<Record<FormField, string>>>({});

  useEffect(() => {
    if (!guildHome?.memberPermissions.canCreateGuildRaids && visibility === "GUILD") {
      setVisibility("PUBLIC");
    }
  }, [guildHome?.memberPermissions.canCreateGuildRaids, visibility]);

  useEffect(() => {
    api.get<WowInstance[]>("/instances")
      .then((res) => setInstances(normalizeWowInstances(res.data)))
      .catch(() => setError("createRaid.loadInstancesFailed"))
      .finally(() => setLoading(false));
  }, []);

  const currentExpansionId = instances.length
    ? Math.max(...instances.map((i) => i.expansionId))
    : null;

  const filteredInstances = currentExpansionId !== null
    ? instances.filter((i) => i.expansionId === currentExpansionId)
    : [];

  const selectedInstance = filteredInstances.find((i) => i.id === instanceId);
  const availableModes = selectedInstance?.modes ?? [];

  const handleInstanceChange = (newId: number) => {
    setInstanceId(newId);
    setSelectedModeKey("");
    if (errors.instance) setErrors((e) => ({ ...e, instance: undefined }));
  };

  const handleModeChange = (newKey: string) => {
    setSelectedModeKey(newKey);
    if (errors.mode) setErrors((e) => ({ ...e, mode: undefined }));
  };

  const handleStartTimeChange = (value: DateTime | null) => {
    setStartTime(value);
    // Re-validate both startTime and signupCloseTime — the cross-field rule references both
    const fieldErrors = validateRaidForm({ instanceId, selectedModeKey, startTime: value, signupCloseTime, description });
    setErrors((e) => ({ ...e, startTime: fieldErrors.startTime, signupCloseTime: fieldErrors.signupCloseTime }));
  };

  const handleSignupCloseTimeChange = (value: DateTime | null) => {
    setSignupCloseTime(value);
    const fieldErrors = validateRaidForm({ instanceId, selectedModeKey, startTime, signupCloseTime: value, description });
    setErrors((e) => ({ ...e, signupCloseTime: fieldErrors.signupCloseTime }));
  };

  const handleDescriptionChange = (value: string) => {
    setDescription(value);
    if (value.trim().length <= MAX_DESCRIPTION) {
      setErrors((e) => ({ ...e, description: undefined }));
    }
  };

  const handleSubmit = async () => {
    const fieldErrors = validateRaidForm({ instanceId, selectedModeKey, startTime, signupCloseTime, description });
    if (Object.keys(fieldErrors).length > 0) {
      setErrors(fieldErrors);
      return;
    }

    const sanitizedDescription = DOMPurify.sanitize(description.trim(), {
      ALLOWED_TAGS: [],
      ALLOWED_ATTR: [],
    });

    setSubmitting(true);
    setError(null);

    try {
      const res = await api.post<{ id: string }>("/raids", {
        instanceId,
        instanceName: selectedInstance!.name,
        startTime: startTime!.toUTC().toISO(),
        ...(signupCloseTime?.isValid ? { signupCloseTime: signupCloseTime.toUTC().toISO() } : {}),
        description: sanitizedDescription,
        modeKey: selectedModeKey,
        visibility,
      });
      navigate(`/raids?raid=${encodeURIComponent(res.data.id)}`);
    } catch {
      setError("createRaid.createFailed");
      setSubmitting(false);
    }
  };

  if (loading) return <Typography sx={{ p: 4 }}>{t("createRaid.loading")}</Typography>;

  const descriptionCount = description.trim().length;

  return (
    <PageContainer maxWidth={600}>
      <Typography variant="h5" component="h1" gutterBottom>{t("createRaid.title")}</Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{t(error)}</Alert>}

      <FormControl fullWidth sx={{ mb: 2 }} error={!!errors.instance}>
        <InputLabel>{t("createRaid.instance")}</InputLabel>
        <Select
          value={instanceId}
          label={t("createRaid.instance")}
          onChange={(e) => handleInstanceChange(e.target.value as number)}
        >
          {filteredInstances.map((inst) => (
            <MenuItem key={inst.id} value={inst.id}>{inst.name}</MenuItem>
          ))}
        </Select>
        {errors.instance && <FormHelperText>{errors.instance}</FormHelperText>}
      </FormControl>

      <FormControl fullWidth sx={{ mb: 2 }} disabled={availableModes.length === 0} error={!!errors.mode}>
        <InputLabel>{t("createRaid.mode")}</InputLabel>
        <Select
          value={selectedModeKey}
          label={t("createRaid.mode")}
          onChange={(e) => handleModeChange(e.target.value)}
        >
          {availableModes.map((mode) => (
            <MenuItem key={toModeKey(mode)} value={toModeKey(mode)}>
              {formatInstanceModeLabel(mode)}
            </MenuItem>
          ))}
        </Select>
        {errors.mode && <FormHelperText>{errors.mode}</FormHelperText>}
      </FormControl>

      <LocalizationProvider dateAdapter={AdapterLuxon} adapterLocale={guildHome?.setup.locale ?? "en"}>
        <DateTimeInput
          label={t("createRaid.startTime")}
          value={startTime}
          onChange={handleStartTimeChange}
          error={errors.startTime}
          required
          disablePast
          timezone={guildHome?.setup.timezone}
        />

        <DateTimeInput
          label={t("createRaid.signupCloseTime")}
          value={signupCloseTime}
          onChange={handleSignupCloseTimeChange}
          error={errors.signupCloseTime}
          disablePast
          maxDateTime={startTime?.isValid ? startTime : undefined}
          timezone={guildHome?.setup.timezone}
        />
      </LocalizationProvider>

      <TextField
        label={t("createRaid.description")}
        multiline
        rows={3}
        fullWidth
        sx={{ mb: 2 }}
        value={description}
        onChange={(e) => handleDescriptionChange(e.target.value)}
        onBlur={() => {
          const fieldErrors = validateRaidForm({ instanceId, selectedModeKey, startTime, signupCloseTime, description });
          setErrors((e) => ({ ...e, description: fieldErrors.description }));
        }}
        error={!!errors.description}
        helperText={
          errors.description
            ? errors.description
            : t("createRaid.descriptionCount", { count: descriptionCount, max: MAX_DESCRIPTION })
        }
        slotProps={{ htmlInput: { maxLength: MAX_DESCRIPTION + 100 } }}
      />

      <Box sx={{ mb: 3 }}>
        <Typography variant="body2" sx={{ mb: 1 }}>{t("createRaid.visibility")}</Typography>
        <ToggleButtonGroup
          exclusive
          value={visibility}
          onChange={(_, newValue: "GUILD" | "PUBLIC" | null) => {
            if (newValue) setVisibility(newValue);
          }}
        >
          <ToggleButton value="PUBLIC">{t("createRaid.public")}</ToggleButton>
          {guildHome?.memberPermissions.canCreateGuildRaids && (
            <ToggleButton value="GUILD">{t("createRaid.guild")}</ToggleButton>
          )}
        </ToggleButtonGroup>
      </Box>

      <Box sx={{ display: "flex", gap: 2 }}>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={submitting}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {t("createRaid.submit")}
        </Button>
        <Button variant="text" onClick={() => navigate("/raids")}>
          {t("createRaid.cancel")}
        </Button>
      </Box>
    </PageContainer>
  );
}
