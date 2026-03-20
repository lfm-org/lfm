import { normalizeLocalizedString } from "../localizedStrings";

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

export function toModeKey(mode: WowInstanceMode): string {
  return `${mode.mode.type}:${mode.players ?? 0}`;
}

export function normalizeWowInstanceMode(mode: WowInstanceMode): WowInstanceMode {
  return {
    ...mode,
    mode: {
      ...mode.mode,
      name: normalizeLocalizedString(mode.mode.name) || mode.mode.type,
    },
  };
}

export function normalizeWowInstance(instance: WowInstance): WowInstance {
  return {
    ...instance,
    name: normalizeLocalizedString(instance.name),
    modes: instance.modes.map(normalizeWowInstanceMode),
  };
}

export function normalizeWowInstances(instances: WowInstance[]): WowInstance[] {
  return instances.map(normalizeWowInstance);
}

export function formatInstanceModeLabel(mode: WowInstanceMode): string {
  const players = mode.players ?? 0;
  const modeName = normalizeLocalizedString(mode.mode.name) || mode.mode.type;
  return `${modeName} (${players} ${players === 1 ? "player" : "players"})`;
}

export function findInstanceMode(
  instances: WowInstance[],
  instanceId: number,
  modeKey: string
): WowInstanceMode | undefined {
  const mode = instances
    .find((instance) => instance.id === instanceId)
    ?.modes.find((entry) => toModeKey(entry) === modeKey);

  return mode ? normalizeWowInstanceMode(mode) : undefined;
}

export function resolveInstanceModeLabel(
  instances: WowInstance[],
  instanceId: number,
  modeKey: string
): string {
  const mode = findInstanceMode(instances, instanceId, modeKey);
  return mode ? formatInstanceModeLabel(mode) : modeKey;
}
