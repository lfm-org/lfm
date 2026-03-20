import type { WowInstance, WowInstanceMode } from "../types/index.js";

export function toModeKey(mode: Pick<WowInstanceMode, "mode" | "players">): string {
  return `${mode.mode.type}:${mode.players ?? 0}`;
}

export function findModeByKey(instance: WowInstance, modeKey: string): WowInstanceMode | undefined {
  return instance.modes.find((mode) => toModeKey(mode) === modeKey);
}

export function hasModeKey(instance: WowInstance, modeKey: string): boolean {
  return findModeByKey(instance, modeKey) !== undefined;
}

export function getModePlayers(mode: Pick<WowInstanceMode, "players">): number {
  return mode.players ?? 0;
}
