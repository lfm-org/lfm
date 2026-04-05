import { useState } from "react";
import { Alert, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useQueryClient } from "@tanstack/react-query";
import api from "../../../lib/api";
import { queryKeys } from "../../../lib/queryKeys";

interface RunDeleteDialogProps {
  open: boolean;
  runId: string;
  runName: string;
  onClose: () => void;
  onDeleted: (runId: string) => void;
}

export default function RunDeleteDialog({ open, runId, runName, onClose, onDeleted }: RunDeleteDialogProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState(false);

  const handleDelete = async () => {
    setDeleting(true);
    setError(false);
    try {
      await api.delete(`/runs/${encodeURIComponent(runId)}`);
      onDeleted(runId);
      void queryClient.invalidateQueries({ queryKey: queryKeys.runs() });
    } catch {
      setError(true);
      setDeleting(false);
    }
  };

  return (
    <Dialog open={open} onClose={deleting ? undefined : onClose}>
      <DialogTitle>{t("runInfo.deleteConfirmTitle")}</DialogTitle>
      <DialogContent>
        <DialogContentText>
          {t("runInfo.deleteConfirmBody", { runName })}
        </DialogContentText>
        {error && <Alert severity="error" sx={{ mt: 1.5 }}>{t("runInfo.deleteFailed")}</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={deleting}>
          {t("runInfo.deleteConfirmCancel")}
        </Button>
        <Button
          onClick={handleDelete}
          color="error"
          variant="contained"
          disabled={deleting}
          startIcon={deleting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {t("runInfo.deleteConfirmDelete")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
