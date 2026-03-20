import type { WowInstance, WowInstanceMode } from "../types/index.js";

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function toModeKey(type: string, players: number): string {
  return `${type}:${players}`;
}

function normalizeWowInstanceMode(mode: unknown): WowInstanceMode | null {
  if (!isRecord(mode)) return null;

  const nestedMode = isRecord(mode.mode) ? mode.mode : null;
  const type = typeof mode.type === "string" ? mode.type : typeof nestedMode?.type === "string" ? nestedMode.type : null;
  const name = typeof mode.name === "string" ? mode.name : typeof nestedMode?.name === "string" ? nestedMode.name : null;
  const players = typeof mode.players === "number" ? mode.players : null;

  if (!type || !name || players === null) {
    return null;
  }

  return {
    type,
    name,
    players,
    isTracked:
      typeof mode.isTracked === "boolean" ? mode.isTracked : typeof mode.is_tracked === "boolean" ? mode.is_tracked : false,
    modeKey: typeof mode.modeKey === "string" && mode.modeKey.length > 0 ? mode.modeKey : toModeKey(type, players),
  };
}

function normalizeWowInstance(instance: unknown): WowInstance | null {
  if (!isRecord(instance) || !Array.isArray(instance.modes)) return null;
  if (
    typeof instance.id !== "number" ||
    typeof instance.name !== "string" ||
    typeof instance.type !== "string" ||
    typeof instance.minLevel !== "number" ||
    typeof instance.expansionId !== "number"
  ) {
    return null;
  }

  return {
    id: instance.id,
    name: instance.name,
    type: instance.type,
    minLevel: instance.minLevel,
    expansionId: instance.expansionId,
    modes: instance.modes
      .map((mode) => normalizeWowInstanceMode(mode))
      .filter((mode): mode is WowInstanceMode => mode !== null),
  };
}

export function normalizeWowInstances(instances: unknown): WowInstance[] {
  if (!Array.isArray(instances)) return [];

  return instances
    .map((instance) => normalizeWowInstance(instance))
    .filter((instance): instance is WowInstance => instance !== null);
}
