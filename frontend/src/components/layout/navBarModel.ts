export interface NavRouteItem {
  i18nKey: string;
  to: string;
}

export function getLoginHref(pathname: string, search: string): string {
  const redirectPath =
    pathname === "/" || pathname.startsWith("/login")
      ? "/runs"
      : `${pathname}${search}`;

  return `/login?redirect=${encodeURIComponent(redirectPath)}`;
}

export function getPrimaryNavItems(): NavRouteItem[] {
  return [
    { i18nKey: "nav.runs", to: "/runs" },
    { i18nKey: "nav.guild", to: "/guild" },
  ];
}

export function getAccountMenuRouteItems(
  isSiteAdmin: boolean,
): NavRouteItem[] {
  return [
    { i18nKey: "nav.characters", to: "/characters" },
    ...(isSiteAdmin
      ? [{ i18nKey: "nav.guildAdmin", to: "/guild/admin" }]
      : []),
  ];
}
