import { Injectable } from "@nestjs/common";
import { InjectRepository } from "@nestjs/typeorm";
import { Brackets, Repository } from "typeorm";
import { Raid, RaidVisibility } from "./raid.entity";

@Injectable()
export class RaidsService {
  constructor(
    @InjectRepository(Raid)
    private readonly raidsRepository: Repository<Raid>
  ) {}

  public findAll(raiderGuild?: string): Promise<Raid[]> {
    const query = this.raidsRepository
      .createQueryBuilder("raid")
      .leftJoinAndSelect("raid.instance", "instance")
      .leftJoinAndSelect("raid.creator", "creator");
    if (raiderGuild) {
      query.where(
        new Brackets((qb) => {
          qb.where("raid.visibility = :public", {
            public: RaidVisibility.PUBLIC,
          }).orWhere(
            new Brackets((inner) => {
              inner
                .where("raid.visibility = :guild", {
                  guild: RaidVisibility.GUILD,
                })
                .andWhere("raid.creator_guild = :guildName", {
                  guildName: raiderGuild,
                });
            })
          );
        })
      );
    } else {
      query.where("raid.visibility = :public", {
        public: RaidVisibility.PUBLIC,
      });
    }
    return query.getMany();
  }

  public async findOne(
    id: number,
    raiderGuild?: string
  ): Promise<Raid | null> {
    const query = this.raidsRepository
      .createQueryBuilder("raid")
      .leftJoinAndSelect("raid.instance", "instance")
      .leftJoinAndSelect("raid.raidCharacters", "raidCharacters")
      .leftJoinAndSelect("raidCharacters.character", "character")
      .leftJoinAndSelect("raid.creator", "creator")
      .where("raid.id = :id", { id });

    if (raiderGuild) {
      query.andWhere(
        new Brackets((qb) => {
          qb.where("raid.visibility = :public", {
            public: RaidVisibility.PUBLIC,
          }).orWhere(
            new Brackets((inner) => {
              inner
                .where("raid.visibility = :guild", {
                  guild: RaidVisibility.GUILD,
                })
                .andWhere("raid.creator_guild = :guildName", {
                  guildName: raiderGuild,
                });
            })
          );
        })
      );
    } else {
      query.andWhere("raid.visibility = :public", {
        public: RaidVisibility.PUBLIC,
      });
    }

    return query.getOne();
  }

  public async create(raid: Raid): Promise<void> {
    if (raid.creator && raid.creator.guildName) {
      raid.creatorGuild = raid.creator.guildName;
    }
    const entity = Object.assign(this.raidsRepository.create(), raid);
    await this.raidsRepository.save(entity);
  }

  public async remove(raid: Raid): Promise<void> {
    await this.raidsRepository.delete(raid.id);
  }
}
