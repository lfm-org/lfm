import { createModel } from "@rematch/core";
import { RootModel } from "./root.model";

export const enum UserLoginState {
  LoggedOut,
  LoggingIn,
  LoggedIn,
  LoggingOut,
}

type UserState = {
  accessToken?: string;
  name?: string;
  redirectUrl?: string;
  loginState: UserLoginState;
};

const resetUserState = () => {
  return { loginState: UserLoginState.LoggedOut } as UserState;
};

export const user = createModel<RootModel>()({
  state: resetUserState(),
  reducers: {
    startLogin(state: UserState, redirectUrl: string) {
      return {
        ...state,
        accessToken: undefined,
        name: undefined,
        redirectUrl: redirectUrl,
        loginState: UserLoginState.LoggingIn,
      } as UserState;
    },
    login(state: UserState, accessToken: string, name?: string) {
      return {
        ...state,
        accessToken: accessToken,
        name: name,
        redirectUrl: undefined,
        loginState: UserLoginState.LoggedIn,
      } as UserState;
    },
    logout(state: UserState) {
      return resetUserState();
    },
  },
});
