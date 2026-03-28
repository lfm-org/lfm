import tseslint from "typescript-eslint";
import security from "eslint-plugin-security";

export default tseslint.config(
  { ignores: ["dist"] },
  {
    files: ["src/**/*.ts"],
    ignores: ["src/scripts/**"],
    languageOptions: {
      ecmaVersion: 2022,
      parser: tseslint.parser,
    },
    plugins: { security },
    rules: {
      ...security.configs.recommended.rules,
      // detect-object-injection produces false positives on typed bracket access
      "security/detect-object-injection": "off",
    },
  },
);
