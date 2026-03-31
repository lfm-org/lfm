export interface NavRouteItem {
  i18nKey: string;
  to: string;
}

export function getLoginHref(pathname: string, search: string): string {
  const redirectPath =
    pathname === "/" || pathname.startsWith("/login")
      ? "/raids"
      : `${pathname}${search}`;

  return `/login?redirect=${encodeURIComponent(redirectPath)}`;
}

export function getPrimaryNavItems(isSiteAdmin: boolean): NavRouteItem[] {
  return [
    { i18nKey: "nav.raids", to: "/raids" },
    { i18nKey: "nav.guild", to: "/guild" },
    ...(isSiteAdmin
      ? [{ i18nKey: "nav.guildAdmin", to: "/guild/admin" }]
      : []),
  ];
}

export function getAccountMenuRouteItems(
  isSiteAdmin: boolean,
): NavRouteItem[] {
  return [
    ...getPrimaryNavItems(isSiteAdmin),
    { i18nKey: "nav.characters", to: "/characters" },
  ];
}
