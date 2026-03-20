import { app, HttpRequest, HttpResponseInit, InvocationContext, Timer } from "@azure/functions";
import { syncBlizzardReferenceData } from "../lib/reference-sync-blizzard.js";
import { jsonResponse } from "../middleware/security-headers.js";

export async function syncEntities(context: InvocationContext): Promise<{ results: Array<{ name: string; status: string }> }> {
  return syncBlizzardReferenceData({
    log: (message) => context.log(message),
  });
}

async function httpHandler(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
  const result = await syncEntities(context);
  return jsonResponse(result);
}

app.http("wow-update", {
  methods: ["POST"],
  route: "wow/update",
  authLevel: "function",
  handler: httpHandler,
});

async function timerHandler(timer: Timer, context: InvocationContext): Promise<void> {
  await syncEntities(context);
}

app.timer("wow-update-timer", {
  schedule: "0 0 6 * * 1",
  handler: timerHandler,
});
