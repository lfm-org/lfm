import { Controller, Post, Redirect, Req } from "@nestjs/common";
import { GoogleService } from "./google.service";
import { Request } from "express";

@Controller("google")
export class GoogleController {
  constructor(private readonly googleService: GoogleService) {}

  // TODO: host alias requires resolve???
  @Post("login")
  @Redirect(
    `${process.env.FRONTEND_SCHEME || "http"}://${process.env.FRONTEND_HOST}:${
      process.env.FRONTEND_PORT
    }/login/failed`,
    302
  )
  public async login(@Req() req: Request) {
    const response = await this.googleService.login(req);
    if (response !== null) {
      const endpoint = `${process.env.FRONTEND_SCHEME || "http"}://${
        process.env.FRONTEND_HOST
      }:${process.env.FRONTEND_PORT}/login/success?access_token=${
        response.accessToken
      }&name=${response.name || ""}`;
      return {
        url: endpoint,
      };
    }
  }
}
