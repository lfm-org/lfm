import { defineConfig } from "vitest/config";
import path from "path";

export default defineConfig({
  ssr: false,
  cacheDir: path.resolve(__dirname, ".cache/vitest"),
  test: {
    environment: "node",
    passWithNoTests: true,
  },
});
