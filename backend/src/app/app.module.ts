import { Module } from "@nestjs/common";
import { TypeOrmModule } from "@nestjs/typeorm";
import { CharactersModule } from "src/characters/characters.module";
import { AuthModule } from "src/auth/auth.module";
import { WoWModule } from "src/wow/wow.module";
import { RaidsModule } from "src/raids/raids.module";
import { RaidersModule } from "src/raiders/raiders.module";

@Module({
  imports: [
    TypeOrmModule.forRoot(),
    CharactersModule,
    AuthModule,
    RaidersModule,
    RaidsModule,
    WoWModule
  ]
})
export class AppModule {}
