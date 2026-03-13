import { Request } from "express";
import { BattleNetIdentity } from "./battle-net-identity.interface";

export interface BattleNetRequest extends Request {
  battleNetIdentity?: BattleNetIdentity;
}
