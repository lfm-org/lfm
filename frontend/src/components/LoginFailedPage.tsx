import React from "react";
import { Button, Typography } from "@material-ui/core";
import { Link as RouterLink } from "react-router-dom";
import "./LoginPage.css";

export function LoginFailedPage() {
  return (
    <div className="LoginPage">
      <Typography variant="h5" component="h1" gutterBottom>
        Sign in failed
      </Typography>
      <Typography gutterBottom>
        Something went wrong during Battle.net authentication. Please try again.
      </Typography>
      <Button component={RouterLink} to="/login" variant="outlined" color="primary">
        Retry login
      </Button>
    </div>
  );
}
