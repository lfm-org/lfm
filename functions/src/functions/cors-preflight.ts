import { app } from "@azure/functions";

const APP_ORIGIN = (() => {
  try {
    return new URL(process.env.APP_BASE_URL || "http://localhost:5173").origin;
  } catch {
    return "http://localhost:5173";
  }
})();

app.http("cors-preflight", {
  methods: ["OPTIONS"],
  route: "{*route}",
  authLevel: "anonymous",
  handler: () => ({
    status: 204,
    headers: {
      "Access-Control-Allow-Origin": APP_ORIGIN,
      "Access-Control-Allow-Credentials": "true",
      "Access-Control-Allow-Methods": "GET, POST, PUT, DELETE, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type, Authorization",
      "Access-Control-Max-Age": "86400",
    },
  }),
});
