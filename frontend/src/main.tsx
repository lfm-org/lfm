import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router";
import ThemeRegistry from "./components/ThemeRegistry";
import App from "./App";
import "./styles/globals.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ThemeRegistry>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </ThemeRegistry>
  </React.StrictMode>
);
