import "./i18n/i18n";
import React from "react";
import ReactDOM from "react-dom/client";
import { RouterProvider } from "react-router";
import { QueryClientProvider } from "@tanstack/react-query";
import ThemeRegistry from "./components/ThemeRegistry";
import { queryClient } from "./lib/queryClient";
import { router } from "./router";
import "./styles/globals.css";

const isDev = import.meta.env.DEV;

// Lazy-load devtools so they are excluded from production bundles
const ReactQueryDevtools = isDev
  ? React.lazy(() =>
      import("@tanstack/react-query-devtools").then((m) => ({
        default: m.ReactQueryDevtools,
      }))
    )
  : null;

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeRegistry>
        <RouterProvider router={router} />
      </ThemeRegistry>
      {ReactQueryDevtools && (
        <React.Suspense fallback={null}>
          <ReactQueryDevtools initialIsOpen={false} />
        </React.Suspense>
      )}
    </QueryClientProvider>
  </React.StrictMode>
);
