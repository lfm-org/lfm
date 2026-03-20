export interface WowInstanceMode {
  mode: {
    type: string;
    name: string;
  };
  players?: number;
  is_tracked?: boolean;
}

export interface WowInstance {
  id: number;
  name: string;
  type: string;
  minLevel: number;
  expansionId: number;
  modes: WowInstanceMode[];
}

export function toModeKey(mode: Pick<WowInstanceMode, "mode" | "players">): string {
  return `${mode.mode.type}:${mode.players ?? 0}`;
}

export function formatInstanceModeLabel(mode: Pick<WowInstanceMode, "mode" | "players">): string {
  const players = mode.players ?? 0;
  return `${mode.mode.name} (${players} ${players === 1 ? "player" : "players"})`;
}

export function findInstanceMode(
  instances: WowInstance[],
  instanceId: number,
  modeKey: string
): WowInstanceMode | undefined {
  return instances
    .find((instance) => instance.id === instanceId)
    ?.modes.find((mode) => toModeKey(mode) === modeKey);
}

export function resolveInstanceModeLabel(
  instances: WowInstance[],
  instanceId: number,
  modeKey: string
): string {
  const mode = findInstanceMode(instances, instanceId, modeKey);
  return mode ? formatInstanceModeLabel(mode) : modeKey;
}
