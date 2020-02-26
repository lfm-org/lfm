import { Module } from "@nestjs/common";
import { JwtModule } from "@nestjs/jwt";
import { PassportModule } from "@nestjs/passport";
import { RaidersModule } from "src/raiders/raiders.module";
import { AuthService } from "./auth.service";
import { jwtConstants } from "./constants";
import { JwtStrategy } from "./jwt.strategy";
import { LocalStrategy } from "./local.strategy";

@Module({
  exports: [AuthService],
  imports: [
    RaidersModule,
    PassportModule.register({
      defaultStrategy: "jwt"
    }),
    JwtModule.register({
      secret: jwtConstants.secret,
      signOptions: { expiresIn: "3600s" }
    })
  ],
  providers: [AuthService, LocalStrategy, JwtStrategy]
})
export class AuthModule {}
