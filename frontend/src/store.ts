import { init, RematchDispatch, RematchRootState } from "@rematch/core";
import { models, RootModel } from "./models/root.model";
import storage from "redux-persist/lib/storage";
import persistPlugin from "@rematch/persist";
import loadingPlugin, { ExtraModelsFromLoading } from "@rematch/loading";

type FullModel = ExtraModelsFromLoading<RootModel>;

const persistConfig = {
  key: "root",
  storage,
};

export const store = init<RootModel, FullModel>({
  models,
  plugins: [loadingPlugin(), persistPlugin(persistConfig)],
});

export type Store = typeof store;
export type Dispatch = RematchDispatch<RootModel>;
export type RootState = RematchRootState<RootModel>;
