import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import {
  Box, Typography, TextField, Button, Alert,
  FormControl, InputLabel, Select, MenuItem,
  ToggleButtonGroup, ToggleButton,
} from "@mui/material";
import api from "../../../lib/api";
import { formatInstanceModeLabel, toModeKey, type WowInstance } from "../../../lib/wow/instances";
import PageContainer from "../../../components/layout/PageContainer";

export default function CreateRaidPage() {
  const navigate = useNavigate();

  const [instances, setInstances] = useState<WowInstance[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [instanceId, setInstanceId] = useState<number | "">("");
  const [startTime, setStartTime] = useState("");
  const [signupCloseTime, setSignupCloseTime] = useState("");
  const [description, setDescription] = useState("");
  const [selectedModeKey, setSelectedModeKey] = useState("");
  const [visibility, setVisibility] = useState<"GUILD" | "PUBLIC">("GUILD");

  useEffect(() => {
    api.get<WowInstance[]>("/instances")
      .then((res) => setInstances(res.data))
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
  };

  const handleSubmit = async () => {
    if (!instanceId || !selectedInstance || !startTime || !selectedModeKey) {
      setError("Instance, start time, and mode are required");
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const res = await api.post<{ id: string }>("/raids", {
        instanceId,
        instanceName: selectedInstance.name,
        startTime,
        ...(signupCloseTime ? { signupCloseTime } : {}),
        description,
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

  return (
    <PageContainer maxWidth={600}>
      <Typography variant="h5" component="h1" gutterBottom>Create Raid</Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <FormControl fullWidth sx={{ mb: 2 }}>
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
      </FormControl>

      <FormControl fullWidth sx={{ mb: 2 }} disabled={availableModes.length === 0}>
        <InputLabel>Mode</InputLabel>
        <Select
          value={selectedModeKey}
          label="Mode"
          onChange={(e) => setSelectedModeKey(e.target.value)}
        >
          {availableModes.map((mode) => (
            <MenuItem key={toModeKey(mode)} value={toModeKey(mode)}>
              {formatInstanceModeLabel(mode)}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <TextField
        label="Start Time"
        type="datetime-local"
        fullWidth
        sx={{ mb: 2 }}
        slotProps={{ inputLabel: { shrink: true } }}
        value={startTime}
        onChange={(e) => setStartTime(e.target.value)}
      />

      <TextField
        label="Signup Close Time"
        type="datetime-local"
        fullWidth
        sx={{ mb: 2 }}
        slotProps={{ inputLabel: { shrink: true } }}
        value={signupCloseTime}
        onChange={(e) => setSignupCloseTime(e.target.value)}
      />

      <TextField
        label="Description"
        multiline
        rows={3}
        fullWidth
        sx={{ mb: 2 }}
        value={description}
        onChange={(e) => setDescription(e.target.value)}
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
          <ToggleButton value="GUILD">Guild</ToggleButton>
          <ToggleButton value="PUBLIC">Public</ToggleButton>
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
