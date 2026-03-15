"use client";

import { useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Typography } from "@mui/material";
import { Suspense } from "react";
import "../LoginPage.css";

function SuccessContent() {
  const searchParams = useSearchParams();
  const router = useRouter();

  useEffect(() => {
    const redirect = searchParams.get("redirect") || "/raids";
    router.replace(redirect);
  }, [router, searchParams]);

  return (
    <div className="LoginPage">
      <Typography>Signing you in...</Typography>
    </div>
  );
}

export default function LoginSuccessPage() {
  return (
    <Suspense>
      <SuccessContent />
    </Suspense>
  );
}
