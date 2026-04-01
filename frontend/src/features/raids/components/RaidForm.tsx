import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  Box, Typography, TextField, Button, Alert, CircularProgress,
  FormControl, InputLabel, Select, MenuItem,
  ToggleButtonGroup, ToggleButton,
} from "@mui/material";
import FormHelperText from "@mui/material/FormHelperText";
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { AdapterLuxon } from "@mui/x-date-pickers/AdapterLuxon";
import { DateTime } from "luxon";
import {
  formatInstanceModeLabel,
  toModeKey,
  type WowInstance,
} from "../../../lib/wow/instances";
import DateTimeInput from "../../../components/DateTimeInput";
import { validateRaidForm, type FormField } from "../lib/raidValidation";

const MAX_DESCRIPTION = 500;

export interface RaidFormValues {
  instanceId: number;
  instanceName: string;
  startTime: string;
  signupCloseTime?: string;
  description: string;
  modeKey: string;
  visibility: "GUILD" | "PUBLIC";
}

export interface RaidFormInitialValues {
  instanceId: number | "";
  startTime: DateTime | null;
  signupCloseTime: DateTime | null;
  description: string;
  selectedModeKey: string;
  visibility: "GUILD" | "PUBLIC";
}

export interface RaidFormProps {
  initialValues: RaidFormInitialValues;
  instances: WowInstance[];
  locale?: string;
  timezone?: string;
  canCreateGuildRaids: boolean;
  onSubmit: (values: RaidFormValues) => void;
  submitting: boolean;
  error: string | null;
  onCancel: () => void;
  submitLabel: string;
  mode: "create" | "edit";
  lockedFields?: Set<string>;
  lockReason?: string;
}

export default function RaidForm({
  initialValues,
  instances,
  locale,
  timezone,
  canCreateGuildRaids,
  onSubmit,
  submitting,
  error,
  onCancel,
  submitLabel,
  mode,
  lockedFields,
  lockReason,
}: RaidFormProps) {
  const { t } = useTranslation();

  const [instanceId, setInstanceId] = useState<number | "">(initialValues.instanceId);
  const [startTime, setStartTime] = useState<DateTime | null>(initialValues.startTime);
  const [signupCloseTime, setSignupCloseTime] = useState<DateTime | null>(initialValues.signupCloseTime);
  const [description, setDescription] = useState(initialValues.description);
  const [selectedModeKey, setSelectedModeKey] = useState(initialValues.selectedModeKey);
  const [visibility, setVisibility] = useState<"GUILD" | "PUBLIC">(initialValues.visibility);
  const [errors, setErrors] = useState<Partial<Record<FormField, string>>>({});

  useEffect(() => {
    if (!canCreateGuildRaids && visibility === "GUILD") {
      setVisibility("PUBLIC");
    }
  }, [canCreateGuildRaids, visibility]);

  const currentExpansionId = instances.length
    ? Math.max(...instances.map((i) => i.expansionId))
    : null;

  const filteredInstances = currentExpansionId !== null
    ? instances.filter((i) => i.expansionId === currentExpansionId)
    : [];

  const selectedInstance = filteredInstances.find((i) => i.id === instanceId);
  const availableModes = selectedInstance?.modes ?? [];

  const instanceLocked = lockedFields?.has("instanceId") ?? false;
  const startTimeLocked = lockedFields?.has("startTime") ?? false;

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
    const fieldErrors = validateRaidForm(
      { instanceId, selectedModeKey, startTime: value, signupCloseTime, description },
      mode,
    );
    setErrors((e) => ({ ...e, startTime: fieldErrors.startTime, signupCloseTime: fieldErrors.signupCloseTime }));
  };

  const handleSignupCloseTimeChange = (value: DateTime | null) => {
    setSignupCloseTime(value);
    const fieldErrors = validateRaidForm(
      { instanceId, selectedModeKey, startTime, signupCloseTime: value, description },
      mode,
    );
    setErrors((e) => ({ ...e, signupCloseTime: fieldErrors.signupCloseTime }));
  };

  const handleDescriptionChange = (value: string) => {
    setDescription(value);
    if (value.trim().length <= MAX_DESCRIPTION) {
      setErrors((e) => ({ ...e, description: undefined }));
    }
  };

  const handleSubmit = () => {
    const fieldErrors = validateRaidForm(
      { instanceId, selectedModeKey, startTime, signupCloseTime, description },
      mode,
    );
    if (Object.keys(fieldErrors).length > 0) {
      setErrors(fieldErrors);
      return;
    }

    onSubmit({
      instanceId: instanceId as number,
      instanceName: selectedInstance!.name,
      startTime: startTime!.toUTC().toISO()!,
      ...(signupCloseTime?.isValid ? { signupCloseTime: signupCloseTime.toUTC().toISO()! } : {}),
      description: description.trim(),
      modeKey: selectedModeKey,
      visibility,
    });
  };

  const descriptionCount = description.trim().length;

  return (
    <Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{t(error)}</Alert>}

      <FormControl fullWidth sx={{ mb: 2 }} error={!!errors.instance} disabled={instanceLocked}>
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
        {instanceLocked && lockReason
          ? <FormHelperText>{lockReason}</FormHelperText>
          : errors.instance && <FormHelperText>{errors.instance}</FormHelperText>
        }
      </FormControl>

      <FormControl
        fullWidth
        sx={{ mb: 2 }}
        disabled={availableModes.length === 0 || instanceLocked}
        error={!!errors.mode}
      >
        <InputLabel>{t("createRaid.mode")}</InputLabel>
        <Select
          value={selectedModeKey}
          label={t("createRaid.mode")}
          onChange={(e) => handleModeChange(e.target.value)}
        >
          {availableModes.map((m) => (
            <MenuItem key={toModeKey(m)} value={toModeKey(m)}>
              {formatInstanceModeLabel(m)}
            </MenuItem>
          ))}
        </Select>
        {errors.mode && <FormHelperText>{errors.mode}</FormHelperText>}
      </FormControl>

      <LocalizationProvider dateAdapter={AdapterLuxon} adapterLocale={locale ?? "en"}>
        <DateTimeInput
          label={t("createRaid.startTime")}
          value={startTime}
          onChange={handleStartTimeChange}
          error={errors.startTime}
          required
          disablePast
          timezone={timezone}
          disabled={startTimeLocked}
          helperText={startTimeLocked ? lockReason : undefined}
        />

        <DateTimeInput
          label={t("createRaid.signupCloseTime")}
          value={signupCloseTime}
          onChange={handleSignupCloseTimeChange}
          error={errors.signupCloseTime}
          disablePast
          maxDateTime={startTime?.isValid ? startTime : undefined}
          timezone={timezone}
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
          const fieldErrors = validateRaidForm(
            { instanceId, selectedModeKey, startTime, signupCloseTime, description },
            mode,
          );
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
          {canCreateGuildRaids && (
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
          {submitLabel}
        </Button>
        <Button variant="text" onClick={onCancel}>
          {t("createRaid.cancel")}
        </Button>
      </Box>
    </Box>
  );
}
