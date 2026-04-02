import { useEffect, useState } from "react";
import { useUnsavedChanges } from "../../../hooks/useUnsavedChanges";
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
import { validateRunForm, type FormField } from "../lib/runValidation";

const MAX_DESCRIPTION = 500;

export interface CreateRunFormValues {
  instanceId: number;
  instanceName: string;
  startTime: string;
  signupCloseTime?: string;
  description: string;
  modeKey: string;
  visibility: "GUILD" | "PUBLIC";
}

export interface EditRunFormValues {
  instanceId?: number;
  instanceName?: string;
  startTime?: string;
  signupCloseTime?: string;
  description: string;
  modeKey?: string;
  visibility: "GUILD" | "PUBLIC";
}

export interface RunFormInitialValues {
  instanceId: number | "";
  startTime: DateTime | null;
  signupCloseTime: DateTime | null;
  description: string;
  selectedModeKey: string;
  visibility: "GUILD" | "PUBLIC";
}

interface RunFormBaseProps {
  initialValues: RunFormInitialValues;
  instances: WowInstance[];
  locale?: string;
  timezone?: string;
  canCreateGuildRuns: boolean;
  submitting: boolean;
  error: string | null;
  onCancel: () => void;
  submitLabel: string;
}

type RunFormProps = RunFormBaseProps & (
  | { mode: "create"; onSubmit: (values: CreateRunFormValues) => void; lockedFields?: undefined; lockReason?: undefined }
  | { mode: "edit"; onSubmit: (values: EditRunFormValues) => void; lockedFields?: Set<string>; lockReason?: string }
);

export default function RunForm({
  initialValues,
  instances,
  locale,
  timezone,
  canCreateGuildRuns,
  onSubmit,
  submitting,
  error,
  onCancel,
  submitLabel,
  mode,
  lockedFields,
  lockReason,
}: RunFormProps) {
  const { t } = useTranslation();

  const [instanceId, setInstanceId] = useState<number | "">(initialValues.instanceId);
  const [startTime, setStartTime] = useState<DateTime | null>(initialValues.startTime);
  const [signupCloseTime, setSignupCloseTime] = useState<DateTime | null>(initialValues.signupCloseTime);
  const [description, setDescription] = useState(initialValues.description);
  const [selectedModeKey, setSelectedModeKey] = useState(initialValues.selectedModeKey);
  const [visibility, setVisibility] = useState<"GUILD" | "PUBLIC">(initialValues.visibility);
  const [errors, setErrors] = useState<Partial<Record<FormField, string>>>({});

  const isDirty = !submitting && (
    instanceId !== initialValues.instanceId ||
    description !== initialValues.description ||
    selectedModeKey !== initialValues.selectedModeKey ||
    visibility !== initialValues.visibility ||
    startTime?.toISO() !== initialValues.startTime?.toISO() ||
    signupCloseTime?.toISO() !== initialValues.signupCloseTime?.toISO()
  );

  const { dialog: unsavedDialog } = useUnsavedChanges(isDirty);

  useEffect(() => {
    if (!canCreateGuildRuns && visibility === "GUILD") {
      setVisibility("PUBLIC");
    }
  }, [canCreateGuildRuns, visibility]);

  const currentExpansionId = instances.length
    ? Math.max(...instances.map((i) => i.expansionId))
    : null;

  const expansionInstances = currentExpansionId !== null
    ? instances.filter((i) => i.expansionId === currentExpansionId)
    : [];

  // In edit mode, ensure the run's instance is always in the list (it may be from a prior expansion)
  const filteredInstances = instanceId && !expansionInstances.some((i) => i.id === instanceId)
    ? [...expansionInstances, ...instances.filter((i) => i.id === instanceId)]
    : expansionInstances;

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
    const fieldErrors = validateRunForm(
      { instanceId, selectedModeKey, startTime: value, signupCloseTime, description },
      mode,
    );
    setErrors((e) => ({ ...e, startTime: fieldErrors.startTime, signupCloseTime: fieldErrors.signupCloseTime }));
  };

  const handleSignupCloseTimeChange = (value: DateTime | null) => {
    setSignupCloseTime(value);
    const fieldErrors = validateRunForm(
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
    const fieldErrors = validateRunForm(
      { instanceId, selectedModeKey, startTime, signupCloseTime, description },
      mode,
    );
    if (Object.keys(fieldErrors).length > 0) {
      setErrors(fieldErrors);
      requestAnimationFrame(() => {
        document.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
      });
      return;
    }

    const common = {
      ...(signupCloseTime?.isValid ? { signupCloseTime: signupCloseTime.toUTC().toISO()! } : {}),
      description: description.trim(),
      visibility,
    };

    if (mode === "create") {
      onSubmit({
        instanceId: instanceId as number,
        instanceName: selectedInstance!.name,
        modeKey: selectedModeKey,
        startTime: startTime!.toUTC().toISO()!,
        ...common,
      });
    } else {
      onSubmit({
        ...(instanceLocked ? {} : { instanceId: instanceId as number, instanceName: selectedInstance!.name, modeKey: selectedModeKey }),
        ...(startTimeLocked ? {} : { startTime: startTime!.toUTC().toISO()! }),
        ...common,
      });
    }
  };

  const descriptionCount = description.trim().length;

  return (
    <Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{t(error)}</Alert>}

      <FormControl fullWidth sx={{ mb: 2 }} error={!!errors.instance} disabled={instanceLocked} required>
        <InputLabel>{t("createRun.instance")}</InputLabel>
        <Select
          value={instanceId}
          label={t("createRun.instance")}
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
        required
      >
        <InputLabel>{t("createRun.mode")}</InputLabel>
        <Select
          value={selectedModeKey}
          label={t("createRun.mode")}
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
          label={t("createRun.startTime")}
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
          label={t("createRun.signupCloseTime")}
          value={signupCloseTime}
          onChange={handleSignupCloseTimeChange}
          error={errors.signupCloseTime}
          disablePast
          maxDateTime={startTime?.isValid ? startTime : undefined}
          timezone={timezone}
        />
      </LocalizationProvider>

      <TextField
        label={t("createRun.description")}
        multiline
        rows={3}
        fullWidth
        sx={{ mb: 2 }}
        value={description}
        onChange={(e) => handleDescriptionChange(e.target.value)}
        onBlur={() => {
          const fieldErrors = validateRunForm(
            { instanceId, selectedModeKey, startTime, signupCloseTime, description },
            mode,
          );
          setErrors((e) => ({ ...e, description: fieldErrors.description }));
        }}
        error={!!errors.description}
        helperText={
          errors.description
            ? errors.description
            : t("createRun.descriptionCount", { count: descriptionCount, max: MAX_DESCRIPTION })
        }
        slotProps={{ htmlInput: { maxLength: MAX_DESCRIPTION } }}
      />

      <Box sx={{ mb: 3 }}>
        <Typography variant="body2" sx={{ mb: 1 }}>{t("createRun.visibility")}</Typography>
        <ToggleButtonGroup
          exclusive
          value={visibility}
          aria-label={t("createRun.visibility")}
          onChange={(_, newValue: "GUILD" | "PUBLIC" | null) => {
            if (newValue) setVisibility(newValue);
          }}
        >
          <ToggleButton value="PUBLIC">{t("createRun.public")}</ToggleButton>
          {canCreateGuildRuns && (
            <ToggleButton value="GUILD">{t("createRun.guild")}</ToggleButton>
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
          {t("createRun.cancel")}
        </Button>
      </Box>
      {unsavedDialog}
    </Box>
  );
}
