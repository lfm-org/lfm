import { Box, Button, CircularProgress, Stack, SvgIcon, TextField, Typography } from "@mui/material";

interface ForgetMeSectionProps {
  deleteConfirmation: string;
  deleteConfirmationValid: boolean;
  deleteError: string | null;
  deleting: boolean;
  onDeleteConfirmationChange: (value: string) => void;
  onDeleteAccount: () => void;
}

const forgetMeSectionSx = {
  borderTop: 1,
  borderColor: "divider",
  pt: 3,
} as const;

function TrashBinIcon() {
  return (
    <SvgIcon fontSize="inherit" aria-hidden="true">
      <path d="M9 3h6l1 2h4v2H4V5h4l1-2Zm-2 6h2v9H7V9Zm4 0h2v9h-2V9Zm4 0h2v9h-2V9ZM6 21h12a2 2 0 0 0 2-2V7H4v12a2 2 0 0 0 2 2Z" />
    </SvgIcon>
  );
}

export default function ForgetMeSection({
  deleteConfirmation,
  deleteConfirmationValid,
  deleteError,
  deleting,
  onDeleteConfirmationChange,
  onDeleteAccount,
}: ForgetMeSectionProps) {
  return (
    <Box component="section" aria-labelledby="forget-me-heading" sx={forgetMeSectionSx}>
      <Stack spacing={1.5} sx={{ maxWidth: 420 }}>
        <Typography
          component="h2"
          id="forget-me-heading"
          variant="subtitle1"
          sx={{ display: "flex", alignItems: "center", gap: 1, color: "text.secondary" }}
        >
          <Box component="span" sx={{ display: "inline-flex", fontSize: "1rem" }}>
            <TrashBinIcon />
          </Box>
          Forget me
        </Typography>

        <Typography variant="body2" color="text.secondary">
          Permanently delete your stored raider profile, clear your Battle.net session, and remove your raid signups.
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Existing raids stay visible, but you lose access to the deleted account and anything tied to it.
        </Typography>

        <TextField
          size="small"
          label="Type FORGET ME to confirm"
          value={deleteConfirmation}
          onChange={(event) => onDeleteConfirmationChange(event.target.value)}
          autoComplete="off"
          disabled={deleting}
          error={deleteConfirmation.length > 0 && !deleteConfirmationValid}
          helperText={deleteConfirmation.length > 0 && !deleteConfirmationValid ? "Confirmation text must match exactly." : "This action cannot be undone."}
          sx={{
            maxWidth: 320,
            "& .MuiOutlinedInput-root": {
              bgcolor: "background.paper",
              "&.Mui-focused fieldset": {
                borderColor: "error.main",
              },
            },
          }}
        />

        {deleteError && (
          <Typography variant="body2" color="error.main" role="alert">
            {deleteError}
          </Typography>
        )}

        <Box>
          <Button
            variant="outlined"
            size="small"
            color="error"
            startIcon={deleting ? <CircularProgress size={16} color="inherit" /> : <TrashBinIcon />}
            disabled={!deleteConfirmationValid || deleting}
            onClick={onDeleteAccount}
          >
            Forget me
          </Button>
        </Box>
      </Stack>
    </Box>
  );
}
