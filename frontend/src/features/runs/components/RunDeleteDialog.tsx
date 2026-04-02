import { useState } from "react";
import { Alert, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from "@mui/material";
import { useTranslation } from "react-i18next";
import api from "../../../lib/api";

interface RaidDeleteDialogProps {
  open: boolean;
  raidId: string;
  raidName: string;
  onClose: () => void;
  onDeleted: (raidId: string) => void;
}

export default function RaidDeleteDialog({ open, raidId, raidName, onClose, onDeleted }: RaidDeleteDialogProps) {
  const { t } = useTranslation();
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState(false);

  const handleDelete = async () => {
    setDeleting(true);
    setError(false);
    try {
      await api.delete(`/raids/${encodeURIComponent(raidId)}`);
      onDeleted(raidId);
    } catch {
      setError(true);
      setDeleting(false);
    }
  };

  return (
    <Dialog open={open} onClose={deleting ? undefined : onClose}>
      <DialogTitle>{t("raidInfo.deleteConfirmTitle")}</DialogTitle>
      <DialogContent>
        <DialogContentText>
          {t("raidInfo.deleteConfirmBody", { raidName })}
        </DialogContentText>
        {error && <Alert severity="error" sx={{ mt: 1.5 }}>{t("raidInfo.deleteFailed")}</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={deleting}>
          {t("raidInfo.deleteConfirmCancel")}
        </Button>
        <Button
          onClick={handleDelete}
          color="error"
          variant="contained"
          disabled={deleting}
          startIcon={deleting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {t("raidInfo.deleteConfirmDelete")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
