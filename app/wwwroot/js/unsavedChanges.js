let enabled = false;

function handler(event) {
  if (!enabled) return;
  event.preventDefault();
  event.returnValue = "";
}

export function setEnabled(next) {
  enabled = Boolean(next);
  globalThis.removeEventListener("beforeunload", handler);
  if (enabled) globalThis.addEventListener("beforeunload", handler);
}
