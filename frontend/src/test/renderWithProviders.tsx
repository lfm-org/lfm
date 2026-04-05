import { render, type RenderOptions } from "@testing-library/react";
import type { ReactElement } from "react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
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

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: 0,
        gcTime: 0,
      },
    },
  });
}

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
  const queryClient = createTestQueryClient();

  const router = createMemoryRouter(
    [{ path: "*", element: ui }],
    { initialEntries: [route] }
  );

  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeRegistry>
        <AuthContext.Provider value={value}>
          <RouterProvider router={router} />
        </AuthContext.Provider>
      </ThemeRegistry>
    </QueryClientProvider>,
    renderOptions,
  );
}
