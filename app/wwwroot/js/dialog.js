// Native <dialog> interop for Blazor WASM. The browser handles focus trap,
// Esc dismissal, inert siblings, and focus restoration — we only need to
// forward showModal() / close() because HTMLDialogElement is not projected
// to .NET.
export function showModal(el) { el.showModal(); }
export function close(el) { el.close(); }
