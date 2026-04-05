import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const apiTarget = process.env.VITE_PROXY_TARGET || "http://127.0.0.1:7071";
const frontendPort = Number.parseInt(process.env.FRONTEND_PORT || "5173", 10);

export default defineConfig({
  plugins: [react()],
  server: {
    port: Number.isFinite(frontendPort) ? frontendPort : 5173,
    proxy: {
      "/api": {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (
            id.includes("node_modules/react-dom/") ||
            id.includes("node_modules/react/") ||
            id.includes("node_modules/react-router/") ||
            id.includes("node_modules/scheduler/") ||
            id.includes("node_modules/@tanstack/react-query/") ||
            id.includes("node_modules/@tanstack/query-core/")
          ) {
            return "vendor-react";
          }
          if (
            id.includes("node_modules/@emotion/")
          ) {
            return "vendor-mui";
          }
          if (
            id.includes("node_modules/@mui/") &&
            !id.includes("node_modules/@mui/x-date-pickers/") &&
            !id.includes("node_modules/@mui/x-internals/")
          ) {
            return "vendor-mui";
          }
        },
      },
    },
  },
});
