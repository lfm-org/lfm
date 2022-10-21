import React, { useEffect } from "react";

export function LoginButtonGoogle() {
  useEffect(() => {
    const gsi = document.createElement("script");
    gsi.src = "https://accounts.google.com/gsi/client";
    gsi.async = true;
    gsi.defer = true;
    document.body.appendChild(gsi);

    return () => {
      document.body.removeChild(gsi);
    };
  }, []);

  return (
    <div
      style={{
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
      }}
    >
      <div
        id="g_id_onload"
        data-client_id="583226176003-jge4ljfh6cahrgn2eevcosnj93vokqce.apps.googleusercontent.com"
        data-context="signin"
        data-ux_mode="popup"
        data-login_uri="http://localhost:3000/google/login"
        data-auto_select="true"
        data-auto_prompt="false"
        data-itp_support="false"
      ></div>
      <div
        className="g_id_signin"
        data-type="standard"
        data-shape="rectangular"
        data-theme="outline"
        data-text="signin_with"
        data-size="large"
        data-logo_alignment="left"
        data-width="200"
      ></div>
    </div>
  );
}
