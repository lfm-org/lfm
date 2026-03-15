"use client";

import { Button, Typography } from "@mui/material";
import { useSearchParams } from "next/navigation";
import { Suspense } from "react";
import "./LoginPage.css";

function LoginContent() {
  const searchParams = useSearchParams();
  const redirectPath = searchParams.get("redirect") || "/raids";
  const endpoint = `/api/battlenet/login?redirect=${encodeURIComponent(redirectPath)}`;

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

export default function LoginPage() {
  return (
    <Suspense>
      <LoginContent />
    </Suspense>
  );
}
