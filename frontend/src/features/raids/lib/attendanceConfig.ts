import type { SxProps, Theme } from "@mui/material/styles";

export type AttendanceStatus = "IN" | "OUT" | "BENCH" | "LATE" | "AWAY";

export interface AttendanceConfig {
  label: string;
  color: string;
  textColor: string;
  chipSx: SxProps<Theme>;
}

export const ATTENDANCE_CONFIG: Record<AttendanceStatus, AttendanceConfig> = {
  IN:    { label: "In",    color: "#2e7d32", textColor: "#fff",                chipSx: { bgcolor: "#2e7d32", color: "#fff" } },
  OUT:   { label: "Out",   color: "#c62828", textColor: "#fff",                chipSx: { bgcolor: "#c62828", color: "#fff" } },
  BENCH: { label: "Bench", color: "#546e7a", textColor: "#fff",                chipSx: { bgcolor: "#546e7a", color: "#fff" } },
  LATE:  { label: "Late",  color: "#f57f17", textColor: "rgba(0,0,0,0.87)",   chipSx: { bgcolor: "#f57f17", color: "rgba(0, 0, 0, 0.87)" } },
  AWAY:  { label: "Away",  color: "#e65100", textColor: "rgba(0,0,0,0.87)",   chipSx: { bgcolor: "#e65100", color: "rgba(0, 0, 0, 0.87)" } },
};

export const ATTENDANCE_OPTIONS = Object.entries(ATTENDANCE_CONFIG).map(
  ([value, cfg]) => ({ value: value as AttendanceStatus, label: cfg.label })
);

export function getAttendanceConfig(status: string): AttendanceConfig {
  return ATTENDANCE_CONFIG[status as AttendanceStatus] ?? {
    label: status,
    color: "#888",
    chipSx: { bgcolor: "#888", color: "#fff" },
  };
}
