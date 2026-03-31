import { useEffect } from "react";
import { useSearchParams, useNavigate } from "react-router";
import { Box, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import SurfaceCard from "../../../components/SurfaceCard";

export default function LoginSuccessPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { t } = useTranslation();

  useEffect(() => {
    const redirect = searchParams.get("redirect") || "/raids";
    navigate(redirect, { replace: true });
  }, [navigate, searchParams]);

  return (
    <Box sx={{ minHeight: "100%", display: "grid", placeItems: "center", px: 2, py: 4 }}>
      <SurfaceCard sx={{ width: "min(100%, 420px)", textAlign: "center" }}>
        <Typography>{t("loginSuccess.signingIn")}</Typography>
      </SurfaceCard>
    </Box>
  );
}
