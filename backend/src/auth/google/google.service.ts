import { Injectable, Req } from "@nestjs/common";
import { OAuth2Client } from "google-auth-library";
import { RaidersService } from "../../raiders/raiders.service";

@Injectable()
export class GoogleService {
  private readonly client: OAuth2Client = new OAuth2Client(
    process.env.GOOGLE_CLIENT_ID
  );

  constructor(private readonly raidersService: RaidersService) {}

  public async login(@Req() req): Promise<LoginResponseDTO | null> {
    const csrfTokenCookie = req.cookies["g_csrf_token"];
    if (csrfTokenCookie === undefined || csrfTokenCookie === null) {
      return null;
    }
    const csrfTokenBody = req.body["g_csrf_token"];
    if (csrfTokenBody === undefined || csrfTokenBody === null) {
      return null;
    }
    if (csrfTokenCookie !== csrfTokenBody) {
      return null;
    }
    const credential = req.body["credential"];
    const ticket = await this.client.verifyIdToken({
      idToken: credential,
      audience: process.env.GOOGLE_CLIENT_ID,
    });
    const payload = ticket.getPayload();
    const name = payload["given_name"] || payload["name"];
    const googleSub = payload["sub"];
    const raider =
      (await this.raidersService.findOneByGoogleSub(googleSub)) ||
      (await this.raidersService.create({
        name: name,
        googleSub: googleSub,
      } as RaiderCreateDTO));
    return {
      name: raider.name,
      accessToken: "1234567890abcdef",
    } as LoginResponseDTO;
  }
}
