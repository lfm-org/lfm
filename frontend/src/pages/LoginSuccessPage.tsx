import { useEffect } from "react";
import { useSearchParams, useNavigate } from "react-router";
import { Typography } from "@mui/material";
import "./LoginPage.css";

export default function LoginSuccessPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  useEffect(() => {
    const redirect = searchParams.get("redirect") || "/raids";
    navigate(redirect, { replace: true });
  }, [navigate, searchParams]);

  return (
    <div className="LoginPage">
      <Typography>Signing you in...</Typography>
    </div>
  );
}
