import js from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";
import jsxA11y from "eslint-plugin-jsx-a11y";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import noUnsanitized from "eslint-plugin-no-unsanitized";

export default tseslint.config(
  { ignores: ["dist", "e2e/test-results"] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ["*.config.{ts,mjs}"],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.node,
    },
  },
  jsxA11y.flatConfigs.recommended,
  {
    files: ["src/**/*.{ts,tsx}"],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: {
      "react-hooks": reactHooks,
      "react-refresh": reactRefresh,
      "no-unsanitized": noUnsanitized,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      // ESLint 10 support currently requires a react-hooks canary that adds new rules.
      "react-hooks/set-state-in-effect": "off",
      "react-refresh/only-export-components": ["warn", { allowConstantExport: true }],
      "no-unsanitized/method": "error",
      "no-unsanitized/property": "error",
    },
  },
  {
    files: ["src/router.tsx"],
    rules: {
      "react-refresh/only-export-components": "off",
    },
  },
);
