import { createTheme } from "@mui/material/styles";

const fontFamily = [
  "-apple-system",
  "BlinkMacSystemFont",
  "\"Segoe UI\"",
  "\"Roboto\"",
  "\"Oxygen\"",
  "\"Ubuntu\"",
  "\"Cantarell\"",
  "\"Fira Sans\"",
  "\"Droid Sans\"",
  "\"Helvetica Neue\"",
  "sans-serif",
].join(",");

/** Attendance status colors used by raid signup chips and roster displays. */
export const attendance = {
  in:    { bg: "#2e7d32", text: "#fff" },
  out:   { bg: "#c62828", text: "#fff" },
  bench: { bg: "#546e7a", text: "#fff" },
  late:  { bg: "#f57f17", text: "rgba(0, 0, 0, 0.87)" },
  away:  { bg: "#bf4400", text: "#fff" },
  unknown: { bg: "#757575", text: "#fff" },
} as const;

/** Subtle surface overlays for decorative cards (dark-theme only). */
export const surface = {
  /** Very faint background tint. */
  tint: "rgba(255, 255, 255, 0.03)",
  /** Slightly stronger background tint for nested cards. */
  tintStrong: "rgba(255, 255, 255, 0.025)",
  /** Subtle border that's softer than the MUI divider token. */
  border: "rgba(255, 255, 255, 0.08)",
  /** Even softer border for nested cards. */
  borderSubtle: "rgba(255, 255, 255, 0.05)",
} as const;

/** Reusable layout tokens consumed by PageContainer and page components. */
export const layout = {
  /** Max content width for standard pages (px). */
  maxWidth: 1100,
  /** Horizontal page gutter (theme spacing units). */
  px: 2,
  /** Default vertical page padding (theme spacing units). */
  py: 3,
  /** Standard gap between page-level grid items (theme spacing units). */
  pageGap: 3,
  /** Standard gap between nested component-level grid items (theme spacing units). */
  componentGap: 2,
} as const;

const theme = createTheme({
  cssVariables: true,
  palette: {
    mode: "dark",
    background: {
      default: "#121212",
      paper: "#1d1d1d",
    },
  },
  typography: {
    fontFamily,
    button: {
      fontWeight: 600,
      textTransform: "none",
    },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          WebkitFontSmoothing: "antialiased",
          MozOsxFontSmoothing: "grayscale",
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: "none",
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        sizeSmall: {
          fontWeight: 600,
          fontSize: "0.7rem",
        },
      },
    },
  },
});

export default theme;
