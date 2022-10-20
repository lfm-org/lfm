interface WoWJournalInstanceIndexEntry {
  id: number;
  name: WoWLocalizedString;
  key: WoWKey;
}

interface WoWJournalInstanceIndex {
  instances: [WoWJournalInstanceIndexEntry];
}

interface WoWJournalInstanceModeEntry {
  mode: WoWJournalInstanceMode;
}

interface WoWJournalInstanceMode {
  name: WoWLocalizedString;
}

const enum WoWJournalInstanceCategoryType {
  DUNGEON = "DUNGEON",
  RAID = "RAID",
  WORLD_BOSS = "WORLD_BOSS",
}

interface WoWJournalInstanceCategory {
  type: WoWJournalInstanceCategoryType;
}

interface WoWJournalInstanceExpansion {
  id: number;
  name: WoWLocalizedString;
  key: WoWKey;
}

interface WoWJournalInstance {
  id: number;
  name: WoWLocalizedString;
  modes?: [WoWJournalInstanceModeEntry];
  minimum_level?: number;
  category: WoWJournalInstanceCategory;
  expansion: WoWJournalInstanceExpansion;
}
