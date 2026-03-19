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
});
