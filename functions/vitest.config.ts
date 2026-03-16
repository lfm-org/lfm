import { defineConfig } from "vitest/config";

export default defineConfig({
  ssr: false,
  test: {
    environment: "node",
    passWithNoTests: true,
  },
});
