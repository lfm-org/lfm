interface WoWPlayableRaceIndexEntry {
  id: number;
  name: WoWLocalizedString;
  key: WoWKey;
}

interface WoWPlayableRaceIndex {
  races: [WoWPlayableRaceIndexEntry];
}

const enum WoWPlayableRaceFactionType {
  HORDE = "HORDE",
  ALLIANCE = "ALLIANCE",
}

interface WoWPlayableRaceFaction {
  type: WoWPlayableRaceFactionType;
}

interface WoWPlayableRace {
  id: number;
  name: WoWLocalizedString;
  faction: WoWPlayableRaceFaction;
}
