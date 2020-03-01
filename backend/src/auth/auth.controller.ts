import { Body, Controller, Post, Request, UseGuards } from "@nestjs/common";
import { AuthService } from "./auth.service";
import { LocalAuthGuard } from "./local-auth.guard";

@Controller("auth")
export class CharactersController {
  constructor(private readonly authService: AuthService) {}

  @UseGuards(LocalAuthGuard)
  @Post("login")
  public async login(@Request() req) {
    return this.authService.login(req.user);
  }

  @Post("register")
  public async register(@Body() body) {
    return this.authService.register(body);
  }
}
