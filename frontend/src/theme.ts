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
