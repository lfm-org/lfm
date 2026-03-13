import { Controller, Get, Query, Redirect } from "@nestjs/common";
import { BattlenetService } from "./battlenet.service";

@Controller("battlenet")
export class BattlenetController {
  constructor(private readonly battlenetService: BattlenetService) {}

  @Get("login")
  @Redirect()
  public login(@Query("redirect") redirect?: string) {
    return { url: this.battlenetService.buildAuthorizationUrl(redirect) };
  }

  @Get("callback")
  @Redirect()
  public async callback(
    @Query("code") code?: string,
    @Query("state") state?: string
  ) {
    const response = await this.battlenetService.handleCallback(code, state);
    if (!response) {
      return { url: this.battlenetService.buildFrontendFailureUrl() };
    }
    return { url: this.battlenetService.buildFrontendSuccessUrl(response) };
  }
}
