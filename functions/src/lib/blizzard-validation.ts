type BattleNetRegion = "eu" | "us" | "kr" | "tw" | "cn";

const VALID_REGIONS = new Set<string>(["eu", "us", "kr", "tw", "cn"]);
const REALM_SLUG_PATTERN = /^[a-z0-9-]+$/;
const CHARACTER_NAME_PATTERN = /^[a-zA-ZÀ-ÿ]+$/;
const MAX_REALM_LENGTH = 64;
const MIN_CHARACTER_NAME = 2;
const MAX_CHARACTER_NAME = 12;

export function validateRegion(region: string): BattleNetRegion {
  if (!VALID_REGIONS.has(region)) {
    throw new Error(`Invalid region: ${region}`);
  }
  return region as BattleNetRegion;
}

export function validateRealmSlug(realm: string): string {
  if (!realm || realm.length > MAX_REALM_LENGTH || !REALM_SLUG_PATTERN.test(realm)) {
    throw new Error(`Invalid realm slug: ${realm}`);
  }
  return realm;
}

export function validateCharacterName(name: string): string {
  if (
    !name ||
    name.length < MIN_CHARACTER_NAME ||
    name.length > MAX_CHARACTER_NAME ||
    !CHARACTER_NAME_PATTERN.test(name)
  ) {
    throw new Error(`Invalid character name: ${name}`);
  }
  return name.toLowerCase();
}

export function encodeBlizzardPathSegments(...segments: string[]): string {
  return segments.map(s => encodeURIComponent(s)).join("/");
}
