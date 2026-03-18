export interface WowInstanceMode {
  type: string;
  name: string;
  players: number;
  isTracked: boolean;
  modeKey: string;
}

export interface WowInstance {
  id: number;
  name: string;
  type: string;
  minLevel: number;
  expansionId: number;
  modes: WowInstanceMode[];
}

export function formatInstanceModeLabel(mode: Pick<WowInstanceMode, "name" | "players">): string {
  return `${mode.name} (${mode.players} ${mode.players === 1 ? "player" : "players"})`;
}

export function findInstanceMode(
  instances: WowInstance[],
  instanceId: number,
  modeKey: string
): WowInstanceMode | undefined {
  return instances
    .find((instance) => instance.id === instanceId)
    ?.modes.find((mode) => mode.modeKey === modeKey);
}

export function resolveInstanceModeLabel(
  instances: WowInstance[],
  instanceId: number,
  modeKey: string
): string {
  const mode = findInstanceMode(instances, instanceId, modeKey);
  return mode ? formatInstanceModeLabel(mode) : modeKey;
}
