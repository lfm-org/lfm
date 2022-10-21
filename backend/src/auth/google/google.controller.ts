import { Controller, Post, Redirect, Req } from "@nestjs/common";
import { GoogleService } from "./google.service";
import { Request } from "express";

@Controller("google")
export class GoogleController {
  constructor(private readonly googleService: GoogleService) {}

  @Post("login")
  @Redirect("http://localhost:3001/login/failed", 302)
  public async login(@Req() req: Request) {
    const response = await this.googleService.login(req);
    if (response !== null) {
      return {
        url: `http://localhost:3001/login/success?access_token=${
          response.accessToken
        }&name=${response.name || ""}`,
      };
    }
  }
}
