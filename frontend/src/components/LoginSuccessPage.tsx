import React, { useEffect } from "react";
import "./LoginPage.css";
import { useNavigate, useSearchParams } from "react-router-dom";
import { store } from "../store";

export function LoginSuccessPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  useEffect(() => {
    const accessToken = searchParams.get("access_token") || undefined;
    const name = searchParams.get("name") || undefined;
    console.log(JSON.stringify(accessToken));
    if (accessToken) {
      const { redirectUrl } = store.getState().user;
      const { dispatch } = store;
      dispatch.user.login(accessToken, name);
      console.log(JSON.stringify(redirectUrl));
      if (redirectUrl) {
        navigate(redirectUrl, { replace: true });
      }
    } else {
      navigate("/login/failed", { replace: true });
    }
  }, [navigate, searchParams]);

  return <div />;
}
