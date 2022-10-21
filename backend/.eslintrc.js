module.exports = {
  env: {
    browser: true,
    es6: true,
    node: true,
  },
  extends: [
    "plugin:@typescript-eslint/eslint-recommended",
    "plugin:@typescript-eslint/recommended",
    "prettier",
    "prettier/@typescript-eslint",
  ],
  parser: "@typescript-eslint/parser",
  parserOptions: {
    project: "tsconfig.json",
    sourceType: "module",
  },
  plugins: [
    "eslint-plugin-jsdoc",
    "eslint-plugin-prefer-arrow",
    "eslint-plugin-import",
    "@typescript-eslint",
    "@typescript-eslint/tslint",
  ],
  root: true,
  rules: {
    "@typescript-eslint/array-type": [
      "error",
      {
        default: "array-simple",
      },
    ],
    "@typescript-eslint/ban-types": [
      "error",
      {
        types: {
          Object: {
            message: "Avoid using the `Object` type. Did you mean `object`?",
          },
          Function: {
            message:
              "Avoid using the `Function` type. Prefer a specific function type, like `() => void`.",
          },
          Boolean: {
            message: "Avoid using the `Boolean` type. Did you mean `boolean`?",
          },
          Number: {
            message: "Avoid using the `Number` type. Did you mean `number`?",
          },
          String: {
            message: "Avoid using the `String` type. Did you mean `string`?",
          },
          Symbol: {
            message: "Avoid using the `Symbol` type. Did you mean `symbol`?",
          },
        },
      },
    ],
    "@typescript-eslint/brace-style": "off",
    "@typescript-eslint/comma-dangle": "off",
    "@typescript-eslint/comma-spacing": "off",
    "@typescript-eslint/consistent-type-definitions": "error",
    "@typescript-eslint/dot-notation": "error",
    "@typescript-eslint/explicit-function-return-type": "off",
    "@typescript-eslint/explicit-member-accessibility": [
      "error",
      {
        accessibility: "explicit",
      },
    ],
    "@typescript-eslint/explicit-module-boundary-types": "off",
    "@typescript-eslint/func-call-spacing": "off",
    "@typescript-eslint/indent": [
      "error",
      4,
      {
        FunctionDeclaration: {
          parameters: "first",
        },
        FunctionExpression: {
          parameters: "first",
        },
      },
    ],
    "@typescript-eslint/interface-name-prefix": "off",
    "@typescript-eslint/keyword-spacing": "off",
    "@typescript-eslint/member-delimiter-style": [
      "error",
      {
        multiline: {
          delimiter: "semi",
          requireLast: true,
        },
        singleline: {
          delimiter: "semi",
          requireLast: false,
        },
      },
    ],
    "@typescript-eslint/member-ordering": "error",
    "@typescript-eslint/naming-convention": [
      "error",
      {
        selector: "variable",
        format: ["camelCase", "UPPER_CASE", "PascalCase"],
        leadingUnderscore: "forbid",
        trailingUnderscore: "forbid",
      },
    ],
    "@typescript-eslint/no-explicit-any": "off",
    "@typescript-eslint/no-extra-parens": "off",
    "@typescript-eslint/no-extra-semi": "off",
    "@typescript-eslint/no-parameter-properties": "off",
    "@typescript-eslint/no-shadow": [
      "error",
      {
        hoist: "all",
      },
    ],
    "@typescript-eslint/no-unused-expressions": "error",
    "@typescript-eslint/no-use-before-define": "off",
    "@typescript-eslint/prefer-for-of": "error",
    "@typescript-eslint/prefer-function-type": "error",
    "@typescript-eslint/quotes": [
      "error",
      "double",
      {
        avoidEscape: true,
      },
    ],
    "@typescript-eslint/semi": ["error", "always"],
    "@typescript-eslint/space-before-function-paren": "off",
    "@typescript-eslint/space-infix-ops": "off",
    "@typescript-eslint/triple-slash-reference": [
      "error",
      {
        path: "always",
        types: "prefer-import",
        lib: "always",
      },
    ],
    "@typescript-eslint/typedef": "off",
    "@typescript-eslint/unified-signatures": "error",
    "arrow-body-style": "error",
    "arrow-parens": ["error", "always"],
    "brace-style": ["error", "1tbs"],
    "comma-dangle": ["error", "always-multiline"],
    complexity: "off",
    "constructor-super": "error",
    curly: "error",
    "dot-notation": "off",
    "eol-last": "error",
    eqeqeq: ["error", "smart"],
    "guard-for-in": "error",
    "id-denylist": [
      "error",
      "any",
      "Number",
      "number",
      "String",
      "string",
      "Boolean",
      "boolean",
      "Undefined",
      "undefined",
    ],
    "id-match": "error",
    "import/order": "error",
    "jsdoc/check-alignment": "error",
    "jsdoc/check-indentation": "error",
    "jsdoc/newline-after-description": "error",
    "max-classes-per-file": ["error", 1],
    "max-len": [
      "error",
      {
        code: 120,
      },
    ],
    "new-parens": "error",
    "no-bitwise": "error",
    "no-caller": "error",
    "no-cond-assign": "error",
    "no-console": "error",
    "no-debugger": "error",
    "no-empty": "error",
    "no-eval": "error",
    "no-fallthrough": "off",
    "no-invalid-this": "off",
    "no-multiple-empty-lines": "error",
    "no-new-wrappers": "error",
    "no-shadow": "off",
    "no-throw-literal": "error",
    "no-trailing-spaces": "error",
    "no-undef-init": "error",
    "no-underscore-dangle": "error",
    "no-unsafe-finally": "error",
    "no-unused-expressions": "off",
    "no-unused-labels": "error",
    "object-shorthand": "error",
    "one-var": ["error", "never"],
    "prefer-arrow/prefer-arrow-functions": "error",
    "quote-props": ["error", "as-needed"],
    radix: "error",
    "space-before-function-paren": [
      "error",
      {
        anonymous: "never",
        asyncArrow: "always",
        named: "never",
      },
    ],
    "spaced-comment": [
      "error",
      "always",
      {
        markers: ["/"],
      },
    ],
    "use-isnan": "error",
    "valid-typeof": "off",
    "@typescript-eslint/tslint/config": [
      "error",
      {
        rules: {
          "import-spacing": true,
          "object-literal-sort-keys": true,
          whitespace: [
            true,
            "check-branch",
            "check-decl",
            "check-operator",
            "check-module",
            "check-separator",
            "check-type",
            "check-typecast",
          ],
        },
      },
    ],
  },
};
