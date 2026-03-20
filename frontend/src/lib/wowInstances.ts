export interface WowInstanceMode {
  mode: {
    type: string;
    name: string;
  };
  players?: number;
  is_tracked?: boolean;
}

interface LegacyWowInstanceMode {
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

type WowInstanceModeLike = LegacyWowInstanceMode;

export interface WowInstance {
  id: number;
  name: string;
  type: string;
  minLevel: number;
  expansionId: number;
  modes: WowInstanceMode[];
}

function getModeType(mode: WowInstanceModeLike): string {
  return mode.mode?.type ?? mode.type ?? mode.modeKey?.split(":")[0] ?? "UNKNOWN";
}

function getModeName(mode: WowInstanceModeLike): string {
  return mode.mode?.name ?? mode.name ?? getModeType(mode);
}

function normalizeMode(mode: WowInstanceModeLike): WowInstanceMode {
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

export function toModeKey(mode: WowInstanceModeLike): string {
  return mode.modeKey ?? `${getModeType(mode)}:${mode.players ?? 0}`;
}

export function formatInstanceModeLabel(mode: WowInstanceModeLike): string {
  const players = mode.players ?? 0;
  return `${getModeName(mode)} (${players} ${players === 1 ? "player" : "players"})`;
}

export function findInstanceMode(
  instances: WowInstance[],
  instanceId: number,
  modeKey: string
): WowInstanceMode | undefined {
  const mode = instances
    .find((instance) => instance.id === instanceId)
    ?.modes.find((entry) => toModeKey(entry) === modeKey);

  return mode ? normalizeMode(mode) : undefined;
}

export function resolveInstanceModeLabel(
  instances: WowInstance[],
  instanceId: number,
  modeKey: string
): string {
  const mode = findInstanceMode(instances, instanceId, modeKey);
  return mode ? formatInstanceModeLabel(mode) : modeKey;
}
