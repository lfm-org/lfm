export interface BlizzardLink {
  href: string;
}

export interface BlizzardLinks {
  self: BlizzardLink;
}

export interface BlizzardLocalizedString {
  en_US?: string;
  en_GB?: string;
  [locale: string]: string | undefined;
}

export interface BlizzardNamedReference {
  id: number;
  name: string;
}

export interface BlizzardPlayableClassIndexEntry {
  key: BlizzardLink;
  id: number;
  name: string;
}

export interface BlizzardPlayableClassIndexResponse {
  _links: BlizzardLinks;
  classes: BlizzardPlayableClassIndexEntry[];
}

export interface BlizzardPlayableClassResponse {
  id: number;
  name: string;
}

export interface BlizzardPlayableRaceIndexEntry {
  key: BlizzardLink;
  id: number;
  name: string;
}

export interface BlizzardPlayableRaceIndexResponse {
  _links: BlizzardLinks;
  races: BlizzardPlayableRaceIndexEntry[];
}

export interface BlizzardPlayableRaceResponse {
  id: number;
  name: string;
  faction?: {
    type: string;
    name?: string;
  };
}

export interface BlizzardPlayableSpecializationIndexEntry {
  key: BlizzardLink;
  id: number;
  name: string;
}

export interface BlizzardPlayableSpecializationIndexResponse {
  _links: BlizzardLinks;
  character_specializations: BlizzardPlayableSpecializationIndexEntry[];
}

export interface BlizzardPlayableSpecializationResponse {
  id: number;
  name: string;
  playable_class: BlizzardNamedReference;
  role: {
    type: "DAMAGE" | "HEALER" | "TANK";
    name: string;
  };
}

export interface BlizzardJournalInstanceIndexEntry {
  key: BlizzardLink;
  id: number;
  name: string | BlizzardLocalizedString;
}

export interface BlizzardJournalInstanceIndexResponse {
  _links: BlizzardLinks;
  instances: BlizzardJournalInstanceIndexEntry[];
}

export interface BlizzardJournalInstanceMode {
  mode: {
    type: string;
    name: string | BlizzardLocalizedString;
  };
  players?: number;
  is_tracked?: boolean;
  is_timewalking?: boolean;
}

export interface BlizzardJournalInstanceResponse {
  id: number;
  name: string | BlizzardLocalizedString;
  category?: {
    type: string;
  };
  expansion?: BlizzardNamedReference;
  minimum_level?: number;
  modes?: BlizzardJournalInstanceMode[];
}

export interface BlizzardAccountCharacterSummary {
  id?: number;
  name: string;
  level: number;
  realm: {
    id?: number;
    slug: string;
    name: string | BlizzardLocalizedString;
  };
  playable_class?: BlizzardNamedReference;
  playable_race?: BlizzardNamedReference;
  faction?: {
    type: string;
    name: string;
  };
  gender?: {
    type: string;
    name: string;
  };
  guild?: { id: number; name?: string };
  protected_character?: BlizzardLink;
}

export interface BlizzardWowAccountSummary {
  id?: number;
  characters?: BlizzardAccountCharacterSummary[];
}

export interface BlizzardAccountProfileSummary {
  wow_accounts?: BlizzardWowAccountSummary[];
}

export interface BlizzardAccountGuildsSummary {
  guilds?: Array<{
    guild?: {
      id?: number;
      name?: string;
    };
  }>;
}

export interface BlizzardUserInfo {
  id: number;
  battletag: string;
}

export interface BlizzardGuildProfileResponse {
  id: number;
  name: string;
  realm: {
    id?: number;
    slug: string;
    name: string | BlizzardLocalizedString;
  };
  motd?: string;
  faction?: { type: string; name?: string };
}

export interface BlizzardCharacterProfileSummary {
  name: string;
  level: number;
  realm: {
    id?: number;
    slug: string;
    name: string | BlizzardLocalizedString;
  };
  character_class: BlizzardNamedReference;
  race: BlizzardNamedReference;
  guild?: { id: number; name?: string };
}

export interface BlizzardCharacterMediaSummary {
  assets?: Array<{
    key: string;
    value: string;
    file_data_id?: number;
  }>;
}

export interface BlizzardCharacterSpecializationsSummary {
  specializations?: Array<{
    specialization: BlizzardNamedReference;
  }>;
  active_specialization?: BlizzardNamedReference;
}
