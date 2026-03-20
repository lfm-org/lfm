import type { WowInstance, WowInstanceMode } from "../types/index.js";

interface WowInstanceModeLike {
  mode?: {
    type?: string;
    name?: string;
  };
  type?: string;
  name?: string;
  players?: number;
  isTracked?: boolean;
  is_tracked?: boolean;
  modeKey?: string;
}

type WowInstanceLike = Omit<WowInstance, "modes"> & { modes: WowInstanceModeLike[] };

function getModeType(mode: WowInstanceModeLike): string {
  return mode.mode?.type ?? mode.type ?? mode.modeKey?.split(":")[0] ?? "UNKNOWN";
}

function getModeName(mode: WowInstanceModeLike): string {
  return mode.mode?.name ?? mode.name ?? getModeType(mode);
}

export function toModeKey(mode: WowInstanceModeLike): string {
  return mode.modeKey ?? `${getModeType(mode)}:${mode.players ?? 0}`;
}

export function normalizeWowInstanceMode(mode: WowInstanceModeLike): WowInstanceMode {
  return {
    mode: {
      type: getModeType(mode),
      name: getModeName(mode),
    },
    ...(mode.players !== undefined ? { players: mode.players } : {}),
    ...((mode.is_tracked ?? mode.isTracked) !== undefined
      ? { is_tracked: mode.is_tracked ?? mode.isTracked }
      : {}),
  };
}

export function normalizeWowInstance(instance: WowInstanceLike): WowInstance {
  return {
    ...instance,
    modes: instance.modes.map(normalizeWowInstanceMode),
  };
}

export function normalizeWowInstances(instances: WowInstanceLike[]): WowInstance[] {
  return instances.map(normalizeWowInstance);
}

export function findModeByKey(instance: WowInstanceLike, modeKey: string): WowInstanceMode | undefined {
  const mode = instance.modes.find((entry) => toModeKey(entry) === modeKey);
  return mode ? normalizeWowInstanceMode(mode) : undefined;
}

export function hasModeKey(instance: WowInstanceLike, modeKey: string): boolean {
  return findModeByKey(instance, modeKey) !== undefined;
}

export function getModePlayers(mode: WowInstanceModeLike): number {
  return mode.players ?? 0;
}
