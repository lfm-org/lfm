import { Module } from "@nestjs/common";
import { TypeOrmModule } from "@nestjs/typeorm";
import { Character } from "./character.entity";
import { WoWModule } from "../wow/wow.module";
import { CharactersController } from "./characters.controller";
import { CharactersService } from "./characters.service";
import { RaidersModule } from "../raiders/raiders.module";

@Module({
  imports: [TypeOrmModule.forFeature([Character]), RaidersModule, WoWModule],
  controllers: [CharactersController],
  providers: [CharactersService],
  exports: [CharactersService],
})
export class CharactersModule {}
