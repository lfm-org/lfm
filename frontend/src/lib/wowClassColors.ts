/** WoW class colors keyed by Battle.net classId (1–13). */
export const WOW_CLASS_COLORS: Record<number, string> = {
  1:  "#C79C6E", // Warrior
  2:  "#F58CBA", // Paladin
  3:  "#AAD372", // Hunter
  4:  "#FFF569", // Rogue
  5:  "#FFFFFF", // Priest
  6:  "#C41E3A", // Death Knight
  7:  "#0070DE", // Shaman
  8:  "#3FC7EB", // Mage
  9:  "#8788EE", // Warlock
  10: "#00FF98", // Monk
  11: "#FF7C0A", // Druid
  12: "#A330C9", // Demon Hunter
  13: "#33937F", // Evoker
};

export function classColor(classId: number): string {
  return WOW_CLASS_COLORS[classId] ?? "#888888";
}
