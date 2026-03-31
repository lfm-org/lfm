import { type ReactNode } from "react";
import { ThemeProvider, createTheme } from "@mui/material/styles";
import CssBaseline from "@mui/material/CssBaseline";
import { useTranslation } from "react-i18next";
import { fiFI as muiFiFI } from "@mui/material/locale";
import theme from "../theme";

const themeEn = theme;
const themeFi = createTheme(theme, muiFiFI);

export default function ThemeRegistry({ children }: { children: ReactNode }) {
  const { i18n } = useTranslation();
  const activeTheme = i18n.language === "fi" ? themeFi : themeEn;

  return (
    <ThemeProvider theme={activeTheme}>
      <CssBaseline />
      {children}
    </ThemeProvider>
  );
}
