import type { SxProps, Theme } from "@mui/material/styles";
import { attendance } from "../../../theme";

export type AttendanceStatus = "IN" | "OUT" | "BENCH" | "LATE" | "AWAY";

export interface AttendanceConfig {
  label: string;
  color: string;
  textColor: string;
  chipSx: SxProps<Theme>;
}

export const ATTENDANCE_CONFIG: Record<AttendanceStatus, AttendanceConfig> = {
  IN:    { label: "In",    color: attendance.in.bg,    textColor: attendance.in.text,    chipSx: { bgcolor: attendance.in.bg,    color: attendance.in.text } },
  OUT:   { label: "Out",   color: attendance.out.bg,   textColor: attendance.out.text,   chipSx: { bgcolor: attendance.out.bg,   color: attendance.out.text } },
  BENCH: { label: "Bench", color: attendance.bench.bg, textColor: attendance.bench.text, chipSx: { bgcolor: attendance.bench.bg, color: attendance.bench.text } },
  LATE:  { label: "Late",  color: attendance.late.bg,  textColor: attendance.late.text,  chipSx: { bgcolor: attendance.late.bg,  color: attendance.late.text } },
  AWAY:  { label: "Away",  color: attendance.away.bg,  textColor: attendance.away.text,  chipSx: { bgcolor: attendance.away.bg,  color: attendance.away.text } },
};

export const ATTENDANCE_OPTIONS = Object.entries(ATTENDANCE_CONFIG).map(
  ([value, cfg]) => ({ value: value as AttendanceStatus, label: cfg.label })
);

const ATTENDING_STATUSES: ReadonlySet<AttendanceStatus> = new Set(["IN", "LATE"]);

export function isAttending(status: AttendanceStatus): boolean {
  return ATTENDING_STATUSES.has(status);
}

export function getAttendanceConfig(status: string): AttendanceConfig {
  return ATTENDANCE_CONFIG[status as AttendanceStatus] ?? {
    label: status,
    color: attendance.unknown.bg,
    chipSx: { bgcolor: attendance.unknown.bg, color: attendance.unknown.text },
  };
}
