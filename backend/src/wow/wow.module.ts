import { HttpModule, Logger, Module } from "@nestjs/common";
import { TypeOrmModule } from "@nestjs/typeorm";
import { Blizzard } from "./blizzard.entity";
import { Class } from "./class.entity";
import { Instance } from "./instance.entity";
import { Race } from "./race.entity";
import { WoWService } from "./wow.service";

@Module({
  exports: [WoWService],
  imports: [
    TypeOrmModule.forFeature([Blizzard, Class, Instance, Race]),
    HttpModule
  ],
  providers: [WoWService]
})
export class WoWModule {
  constructor(private readonly wowService: WoWService) {
    this.wowService.isTimeToUpdate().then(update => {
      if (update) {
        this.wowService
          .auth()
          .then(() => {
            wowService.classes();
            wowService.races();
            wowService.instances();
            wowService.lastUpdated(true);
            Logger.log("Blizzard Update Completed.");
          })
          .catch(() => {
            wowService.lastUpdated(false);
          });
      }
    });
  }
}
