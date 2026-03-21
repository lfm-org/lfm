import { defineConfig } from "vitest/config";
import path from "path";

export default defineConfig({
  cacheDir: path.resolve(".cache/vite"),
  ssr: false,
  test: {
    environment: "node",
    passWithNoTests: true,
  },
});
