import { useEffect, type ReactNode } from "react";
import { useBlocker } from "react-router";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from "@mui/material";
import { useTranslation } from "react-i18next";

export function useUnsavedChanges(isDirty: boolean): { dialog: ReactNode } {
  const blocker = useBlocker(isDirty);
  const { t } = useTranslation();

  useEffect(() => {
    if (!isDirty) return;
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault();
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [isDirty]);

  const dialog =
    blocker.state === "blocked" ? (
      <Dialog open onClose={() => blocker.reset()}>
        <DialogTitle>{t("unsavedChanges.title")}</DialogTitle>
        <DialogContent>
          <DialogContentText>{t("unsavedChanges.body")}</DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => blocker.reset()} variant="contained">
            {t("unsavedChanges.stay")}
          </Button>
          <Button onClick={() => blocker.proceed()} color="error">
            {t("unsavedChanges.leave")}
          </Button>
        </DialogActions>
      </Dialog>
    ) : null;

  return { dialog };
}
