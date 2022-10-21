import { Module } from "@nestjs/common";
import { HttpModule } from "@nestjs/axios";
import { TypeOrmModule } from "@nestjs/typeorm";
import { WoWMeta } from "./meta.entity";
import { WoWClass } from "./class.entity";
import { WoWInstance } from "./instance.entity";
import { WoWRace } from "./race.entity";
import { WoWService } from "./wow.service";

@Module({
  exports: [WoWService],
  imports: [
    TypeOrmModule.forFeature([WoWMeta, WoWClass, WoWInstance, WoWRace]),
    HttpModule,
  ],
  providers: [WoWService],
})
export class WoWModule {
  constructor(private readonly wowService: WoWService) {
    this.wowService.update();
  }
}
