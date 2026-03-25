import { DateTimePicker } from "@mui/x-date-pickers/DateTimePicker";
import { DateTime } from "luxon";
import { GUILD_TIMEZONE } from "../lib/guildConfig";

interface DateTimeInputProps {
  label: string;
  value: DateTime | null;
  onChange: (value: DateTime | null) => void;
  error?: string;
  required?: boolean;
  disablePast?: boolean;
  maxDateTime?: DateTime;
  timezone?: string;
}

export default function DateTimeInput({
  label,
  value,
  onChange,
  error,
  required,
  disablePast,
  maxDateTime,
  timezone = GUILD_TIMEZONE,
}: DateTimeInputProps) {
  return (
    <DateTimePicker
      label={label}
      value={value}
      onChange={onChange}
      disablePast={disablePast}
      maxDateTime={maxDateTime}
      timezone={timezone}
      slotProps={{
        textField: {
          fullWidth: true,
          required,
          error: !!error,
          helperText: error,
          sx: { mb: 2 },
        },
      }}
    />
  );
}
