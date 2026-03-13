import React, { useEffect } from "react";
import { Typography } from "@material-ui/core";
import { useNavigate, useSearchParams } from "react-router-dom";
import "./LoginPage.css";
import {
  setAccessToken,
  setDisplayName,
  setGuildName,
} from "../util/AuthUtil";

export function LoginSuccessPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  useEffect(() => {
    const accessToken = searchParams.get("access_token");
    const name = searchParams.get("name");
    const guild = searchParams.get("guild");
    const redirectPath = searchParams.get("redirect") || "/raids";
    if (accessToken) {
      setAccessToken(accessToken);
      if (name) {
        setDisplayName(name);
      }
      if (guild) {
        setGuildName(guild);
      }
      navigate(redirectPath, { replace: true });
    } else {
      navigate("/login/failed", { replace: true });
    }
  }, [navigate, searchParams]);

  return (
    <div className="LoginPage">
      <Typography>Signing you in...</Typography>
    </div>
  );
}
