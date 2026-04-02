import "./i18n/i18n";
import React from "react";
import ReactDOM from "react-dom/client";
import { RouterProvider } from "react-router";
import ThemeRegistry from "./components/ThemeRegistry";
import { router } from "./router";
import "./styles/globals.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ThemeRegistry>
      <RouterProvider router={router} />
    </ThemeRegistry>
  </React.StrictMode>
);
