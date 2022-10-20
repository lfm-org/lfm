const enum WoWHeaderOptionsRegion {
  US = "us",
  EU = "eu",
  KR = "kr",
  TW = "tw",
  CN = "cn",
}

const enum WoWHeaderOptionsNamespaceCategory {
  STATIC = "static",
  DYNAMIC = "dynamic",
}

interface WoWHeaderOptions {
  region: WoWHeaderOptionsRegion;
  namespaceCategory: WoWHeaderOptionsNamespaceCategory;
  classic: boolean;
}
