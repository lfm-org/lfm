import React, { useEffect, useState } from "react";
import "./LoginPage.css";
import { LoginButtonGoogle } from "./LoginButtonGoogle";
import { store } from "../store";
import { useLocation } from "react-router-dom";
import { UserLoginState } from "../models/user.model";

export function LoginPage() {
  const [loginState, setLoginState] = useState(
    store.getState().user.loginState
  );
  const location = useLocation();
  const redirectUrl = location.pathname;

  useEffect(() => {
    const { dispatch } = store;
    if (store.getState().user.loginState === UserLoginState.LoggedOut) {
      dispatch.user.startLogin(redirectUrl);
    }
    setLoginState(store.getState().user.loginState);
  }, [redirectUrl]);

  return (
    <div className="LoginPage">
      {loginState === UserLoginState.LoggingIn && (
        <>
          <LoginButtonGoogle />
        </>
      )}
    </div>
  );
}
