import { render, type RenderOptions } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import { MemoryRouter } from "react-router";
import ThemeRegistry from "../components/ThemeRegistry";
import { AuthContext, type AuthContextValue } from "../features/auth/lib/context";

const defaultAuthValue: AuthContextValue = {
  user: null,
  loading: false,
  onCharacterSelected: () => {},
  clearAuth: () => {},
  onAccountDeleted: () => {},
  postAuthRedirect: null,
  setLocale: () => Promise.resolve(),
};

interface RenderWithProvidersOptions extends Omit<RenderOptions, "wrapper"> {
  route?: string;
  authValue?: Partial<AuthContextValue>;
}

export function renderWithProviders(
  ui: ReactElement,
  options: RenderWithProvidersOptions = {}
) {
  const { route = "/", authValue, ...renderOptions } = options;
  const value: AuthContextValue = { ...defaultAuthValue, ...authValue };

  return render(ui, {
    wrapper: ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={[route]}>
        <ThemeRegistry>
          <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
        </ThemeRegistry>
      </MemoryRouter>
    ),
    ...renderOptions,
  });
}
