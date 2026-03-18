export type SpecRole = "TANK" | "HEALER" | "DPS";

export const WOW_SPEC_ROLES: Record<number, SpecRole> = {
  // Warrior
  71: "DPS",    // Arms
  72: "DPS",    // Fury
  73: "TANK",   // Protection
  // Paladin
  65: "HEALER", // Holy
  66: "TANK",   // Protection
  70: "DPS",    // Retribution
  // Hunter
  253: "DPS",   // Beast Mastery
  254: "DPS",   // Marksmanship
  255: "DPS",   // Survival
  // Rogue
  259: "DPS",   // Assassination
  260: "DPS",   // Outlaw
  261: "DPS",   // Subtlety
  // Priest
  256: "HEALER", // Discipline
  257: "HEALER", // Holy
  258: "DPS",    // Shadow
  // Death Knight
  250: "TANK",  // Blood
  251: "DPS",   // Frost
  252: "DPS",   // Unholy
  // Shaman
  262: "DPS",    // Elemental
  263: "DPS",    // Enhancement
  264: "HEALER", // Restoration
  // Mage
  62: "DPS",    // Arcane
  63: "DPS",    // Fire
  64: "DPS",    // Frost
  // Warlock
  265: "DPS",   // Affliction
  266: "DPS",   // Demonology
  267: "DPS",   // Destruction
  // Monk
  268: "TANK",   // Brewmaster
  269: "DPS",    // Windwalker
  270: "HEALER", // Mistweaver
  // Druid
  102: "DPS",    // Balance
  103: "DPS",    // Feral
  104: "TANK",   // Guardian
  105: "HEALER", // Restoration
  // Demon Hunter
  577: "DPS",   // Havoc
  581: "TANK",  // Vengeance
  // Evoker
  1467: "DPS",    // Devastation
  1468: "HEALER", // Preservation
  1473: "DPS",    // Augmentation
};

/** Resolve a spec ID to a role. Falls back to DPS for unknown or null IDs. */
export function resolveSpecRole(specId: number | null): SpecRole {
  if (specId == null) return "DPS";
  return WOW_SPEC_ROLES[specId] ?? "DPS";
}
