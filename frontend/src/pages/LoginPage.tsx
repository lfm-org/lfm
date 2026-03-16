import { Button, Typography } from "@mui/material";
import { useSearchParams } from "react-router";
import { getLoginUrl } from "../lib/auth";
import "./LoginPage.css";

export default function LoginPage() {
  const [searchParams] = useSearchParams();
  const redirectPath = searchParams.get("redirect") || "/raids";

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
        href={getLoginUrl(redirectPath)}
      >
        Continue with Battle.net
      </Button>
    </div>
  );
}
