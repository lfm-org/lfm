import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import {
  Box, Typography, TextField, Button, Alert,
  FormControl, InputLabel, Select, MenuItem,
  ToggleButtonGroup, ToggleButton,
} from "@mui/material";
import FormHelperText from "@mui/material/FormHelperText";
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

type FormField = "instance" | "mode" | "startTime" | "signupCloseTime" | "description";

function validate(fields: {
  instanceId: number | "";
  selectedModeKey: string;
  startTime: DateTime | null;
  signupCloseTime: DateTime | null;
  description: string;
}): Partial<Record<FormField, string>> {
  const errors: Partial<Record<FormField, string>> = {};

  if (!fields.instanceId) errors.instance = "Instance is required";
  if (!fields.selectedModeKey) errors.mode = "Mode is required";

  if (!fields.startTime || !fields.startTime.isValid) {
    errors.startTime = "Start time is required";
  } else if (fields.startTime <= DateTime.now()) {
    errors.startTime = "Start time must be in the future";
  }

  if (fields.signupCloseTime?.isValid) {
    if (fields.signupCloseTime <= DateTime.now()) {
      errors.signupCloseTime = "Signup close time must be in the future";
    } else if (fields.startTime?.isValid && fields.signupCloseTime >= fields.startTime) {
      errors.signupCloseTime = "Signup close time must be before start time";
    }
  }

  if (fields.description.trim().length > 500) {
    errors.description = "Description must be 500 characters or fewer";
  }

  return errors;
}

const MAX_DESCRIPTION = 500;

export default function CreateRaidPage() {
  const navigate = useNavigate();
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
      .catch(() => setError("Failed to load instances"))
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
    const fieldErrors = validate({ instanceId, selectedModeKey, startTime: value, signupCloseTime, description });
    setErrors((e) => ({ ...e, startTime: fieldErrors.startTime, signupCloseTime: fieldErrors.signupCloseTime }));
  };

  const handleSignupCloseTimeChange = (value: DateTime | null) => {
    setSignupCloseTime(value);
    const fieldErrors = validate({ instanceId, selectedModeKey, startTime, signupCloseTime: value, description });
    setErrors((e) => ({ ...e, signupCloseTime: fieldErrors.signupCloseTime }));
  };

  const handleDescriptionChange = (value: string) => {
    setDescription(value);
    if (value.trim().length <= MAX_DESCRIPTION) {
      setErrors((e) => ({ ...e, description: undefined }));
    }
  };

  const handleSubmit = async () => {
    const fieldErrors = validate({ instanceId, selectedModeKey, startTime, signupCloseTime, description });
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
      setError("Failed to create raid");
      setSubmitting(false);
    }
  };

  if (loading) return <Typography sx={{ p: 4 }}>Loading...</Typography>;

  const descriptionCount = description.trim().length;

  return (
    <PageContainer maxWidth={600}>
      <Typography variant="h5" component="h1" gutterBottom>Create Raid</Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <FormControl fullWidth sx={{ mb: 2 }} error={!!errors.instance}>
        <InputLabel>Instance</InputLabel>
        <Select
          value={instanceId}
          label="Instance"
          onChange={(e) => handleInstanceChange(e.target.value as number)}
        >
          {filteredInstances.map((inst) => (
            <MenuItem key={inst.id} value={inst.id}>{inst.name}</MenuItem>
          ))}
        </Select>
        {errors.instance && <FormHelperText>{errors.instance}</FormHelperText>}
      </FormControl>

      <FormControl fullWidth sx={{ mb: 2 }} disabled={availableModes.length === 0} error={!!errors.mode}>
        <InputLabel>Mode</InputLabel>
        <Select
          value={selectedModeKey}
          label="Mode"
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

      <DateTimeInput
        label="Start Time"
        value={startTime}
        onChange={handleStartTimeChange}
        error={errors.startTime}
        required
        disablePast
        timezone={guildHome?.setup.timezone}
      />

      <DateTimeInput
        label="Signup Close Time"
        value={signupCloseTime}
        onChange={handleSignupCloseTimeChange}
        error={errors.signupCloseTime}
        disablePast
        maxDateTime={startTime?.isValid ? startTime : undefined}
        timezone={guildHome?.setup.timezone}
      />

      <TextField
        label="Description"
        multiline
        rows={3}
        fullWidth
        sx={{ mb: 2 }}
        value={description}
        onChange={(e) => handleDescriptionChange(e.target.value)}
        onBlur={() => {
          const fieldErrors = validate({ instanceId, selectedModeKey, startTime, signupCloseTime, description });
          setErrors((e) => ({ ...e, description: fieldErrors.description }));
        }}
        error={!!errors.description}
        helperText={
          errors.description
            ? errors.description
            : `${descriptionCount}/${MAX_DESCRIPTION}`
        }
        slotProps={{ htmlInput: { maxLength: MAX_DESCRIPTION + 100 } }}
      />

      <Box sx={{ mb: 3 }}>
        <Typography variant="body2" sx={{ mb: 1 }}>Visibility</Typography>
        <ToggleButtonGroup
          exclusive
          value={visibility}
          onChange={(_, newValue: "GUILD" | "PUBLIC" | null) => {
            if (newValue) setVisibility(newValue);
          }}
        >
          <ToggleButton value="PUBLIC">Public</ToggleButton>
          {guildHome?.memberPermissions.canCreateGuildRaids && (
            <ToggleButton value="GUILD">Guild</ToggleButton>
          )}
        </ToggleButtonGroup>
      </Box>

      <Box sx={{ display: "flex", gap: 2 }}>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={submitting}
        >
          {submitting ? "Creating..." : "Create Raid"}
        </Button>
        <Button variant="text" onClick={() => navigate("/raids")}>
          Cancel
        </Button>
      </Box>
    </PageContainer>
  );
}
