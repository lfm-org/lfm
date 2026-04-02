import { useCallback, useState, type ReactNode } from "react";
import { Alert, Snackbar } from "@mui/material";
import { ToastContext } from "./toastContext";

interface ToastState {
  open: boolean;
  message: string;
  severity: "success" | "error";
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<ToastState>({
    open: false,
    message: "",
    severity: "success",
  });

  const showSuccess = useCallback((message: string) => {
    setToast({ open: true, message, severity: "success" });
  }, []);

  const showError = useCallback((message: string) => {
    setToast({ open: true, message, severity: "error" });
  }, []);

  const handleClose = () => setToast((prev) => ({ ...prev, open: false }));

  return (
    <ToastContext.Provider value={{ showSuccess, showError }}>
      {children}
      <Snackbar
        open={toast.open}
        autoHideDuration={toast.severity === "success" ? 4000 : null}
        onClose={handleClose}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
      >
        <Alert severity={toast.severity} onClose={handleClose} variant="filled">
          {toast.message}
        </Alert>
      </Snackbar>
    </ToastContext.Provider>
  );
}
