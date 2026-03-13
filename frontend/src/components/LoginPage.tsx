import React from "react";
import { Button, Typography } from "@material-ui/core";
import { useLocation } from "react-router-dom";
import { buildApiUrl } from "../util/ApiUtil";
import "./LoginPage.css";

type LocationState = {
  from?: {
    pathname?: string;
  };
};

export function LoginPage() {
  const location = useLocation();
  const state = location.state as LocationState;
  const redirectPath = state?.from?.pathname || "/raids";
  const endpoint = `${buildApiUrl("/battlenet/login")}?redirect=${encodeURIComponent(
    redirectPath
  )}`;

  return (
    <div className="LoginPage">
      <Typography variant="h4" component="h1" gutterBottom>
        Sign in with Battle.net
      </Typography>
      <Typography>
        Continue with your Battle.net account to keep track of raid signups.
      </Typography>
      <Button
        variant="contained"
        color="primary"
        size="large"
        disableElevation
        href={endpoint}
      >
        Continue with Battle.net
      </Button>
    </div>
  );
}
