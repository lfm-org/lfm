import { Box, Button, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useAuth } from "../../features/auth";
import { isSupportedLocale } from "../../i18n/i18n";

const locales = [
  { code: "en", label: "🇬🇧", i18nKey: "footer.switchToEnglish" },
  { code: "fi", label: "🇫🇮", i18nKey: "footer.switchToFinnish" },
] as const;

export default function Footer() {
  const { i18n, t } = useTranslation();
  const { setLocale } = useAuth();

  const handleLocaleChange = (code: string) => {
    if (!isSupportedLocale(code)) return;
    setLocale(code);
  };

  return (
    <Box
      component="footer"
      sx={{
        borderTop: 1,
        borderColor: "divider",
        px: 2,
        py: 1,
        display: "flex",
        justifyContent: "flex-end",
        alignItems: "center",
        gap: 0.5,
      }}
    >
      <Typography
        component={Link}
        to="/privacy"
        variant="caption"
        sx={{
          color: "text.secondary",
          textDecoration: "none",
          "&:hover": { textDecoration: "underline" },
        }}
      >
        {t("footer.privacyPolicy")}
      </Typography>
      <Typography variant="caption" color="text.secondary" sx={{ mx: 0.5 }}>
        ·
      </Typography>
      {locales.map(({ code, label, i18nKey }) => (
        <Button
          key={code}
          size="small"
          aria-label={t(i18nKey)}
          onClick={() => handleLocaleChange(code)}
          disabled={i18n.language === code}
          sx={{
            minWidth: 44,
            minHeight: 44,
            color: i18n.language === code ? "text.primary" : "text.secondary",
            fontWeight: i18n.language === code ? 700 : 400,
            fontSize: "1rem",
          }}
        >
          {label}
        </Button>
      ))}
    </Box>
  );
}
