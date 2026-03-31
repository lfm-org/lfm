export interface NavRouteItem {
  label: string;
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
    { label: "Raids", to: "/raids" },
    { label: "Guild", to: "/guild" },
    ...(isSiteAdmin
      ? [{ label: "Guild Admin", to: "/guild/admin" }]
      : []),
  ];
}

export function getAccountMenuRouteItems({
  isSiteAdmin,
  isCompact,
}: {
  isSiteAdmin: boolean;
  isCompact: boolean;
}): NavRouteItem[] {
  return [
    { label: "Characters", to: "/characters" },
    ...(isCompact ? getPrimaryNavItems(isSiteAdmin) : []),
  ];
}
