import {
  CanActivate,
  ExecutionContext,
  Injectable,
  UnauthorizedException,
} from "@nestjs/common";
import { BattleNetIdentity } from "./battle-net-identity.interface";
import { BattlenetService } from "./battlenet.service";
import { BattleNetRequest } from "./battle-net-request.interface";

@Injectable()
export class BattlenetAuthGuard implements CanActivate {
  constructor(private readonly battlenetService: BattlenetService) {}

  public async canActivate(context: ExecutionContext): Promise<boolean> {
    const request: BattleNetRequest = context.switchToHttp().getRequest();
    const rawAuthorization =
      request.headers?.authorization || request.headers?.Authorization;
    const authorization = Array.isArray(rawAuthorization)
      ? rawAuthorization[0]
      : rawAuthorization;
    if (!authorization?.startsWith("Bearer ")) {
      throw new UnauthorizedException("Battle.net access token required");
    }
    const token = authorization.substring("Bearer ".length);
    const identity = await this.battlenetService.resolveIdentity(token);
    if (!identity) {
      throw new UnauthorizedException("Invalid Battle.net access token");
    }
    request.battleNetIdentity = identity as BattleNetIdentity;
    return true;
  }
}
