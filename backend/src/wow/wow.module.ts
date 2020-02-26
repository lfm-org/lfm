import { Module, HttpModule } from "@nestjs/common";
import { WoWService } from "./wow.service";
import { TypeOrmModule } from "@nestjs/typeorm";
import { Class } from "./class.entity";
import { Race } from "./race.entity";
import { Instance } from "./instance.entity";

@Module({
  imports: [TypeOrmModule.forFeature([Class, Race, Instance]), HttpModule],
  providers: [WoWService],
  exports: [WoWService]
})
export class WoWModule {
  constructor(/* private readonly wowService: WoWService */) {
    // wowService.auth().then(() => {
    //     wowService.classes()
    //     wowService.races()
    //     wowService.instances()
    //     Logger.log("Blizzard Update Completed.")
    // })
  }
}
