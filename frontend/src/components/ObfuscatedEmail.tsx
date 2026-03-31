import { useState } from "react";
import { Button, Typography } from "@mui/material";
import { Email } from "react-obfuscate-email";
import { useTranslation } from "react-i18next";
import api from "../lib/api";

type State = { status: "idle" } | { status: "loading" } | { status: "loaded"; email: string } | { status: "error" };

export default function ObfuscatedEmail() {
  const { t } = useTranslation();
  const [state, setState] = useState<State>({ status: "idle" });

  async function reveal() {
    setState({ status: "loading" });
    try {
      const { data } = await api.get<{ email: string }>("/privacy-contact");
      setState({ status: "loaded", email: data.email });
    } catch {
      setState({ status: "error" });
    }
  }

  if (state.status === "loaded") {
    return <Email email={state.email} />;
  }

  if (state.status === "error") {
    return <Typography color="error">{t("privacy.contact.error")}</Typography>;
  }

  return (
    <Button variant="outlined" size="small" onClick={reveal} disabled={state.status === "loading"}>
      {state.status === "loading" ? t("privacy.contact.revealing") : t("privacy.contact.reveal")}
    </Button>
  );
}
