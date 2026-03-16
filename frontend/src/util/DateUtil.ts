import { format, isPast, parseISO } from "date-fns";

const DEFAULT_FORMAT = "dd.MM.yyyy HH.mm";

export const DateUtils = {
  FormatDate(date: string, fmt = DEFAULT_FORMAT): string {
    return format(parseISO(date), fmt);
  },

  FormatDateWithPassed(date: string, fmt = DEFAULT_FORMAT): string {
    return isPast(parseISO(date)) ? "Passed" : format(parseISO(date), fmt);
  },
};
