interface WoWPlayableClassIndexEntry {
  id: number;
  name: WoWLocalizedString;
  key: WoWKey;
}

interface WoWPlayableClassIndex {
  classes: [WoWPlayableClassIndexEntry];
}

interface WoWPlayableClass {
  id: number;
  name: WoWLocalizedString;
}
