import { Button, Typography } from "@mui/material";
import Link from "next/link";
import "../LoginPage.css";

export default function LoginFailedPage() {
  return (
    <div className="LoginPage">
      <Typography variant="h5" component="h1" gutterBottom>
        Sign in failed
      </Typography>
      <Typography gutterBottom>
        Something went wrong during Battle.net authentication. Please try again.
      </Typography>
      <Button
        component={Link}
        href="/login"
        variant="outlined"
        color="primary"
      >
        Retry login
      </Button>
    </div>
  );
}
