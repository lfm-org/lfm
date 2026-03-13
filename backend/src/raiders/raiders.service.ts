import { Injectable } from "@nestjs/common";
import { InjectRepository } from "@nestjs/typeorm";
import { Repository } from "typeorm";
import { Raider } from "./raider.entity";
import { RaiderCreateDTO } from "./dto/create.dto";

@Injectable()
export class RaidersService {
  constructor(
    @InjectRepository(Raider)
    private readonly raidersRepository: Repository<Raider>
  ) {}

  public findOneById(id: number): Promise<Raider | null> {
    return this.raidersRepository.findOneBy({ id: id });
  }

  public findOneByName(name: string): Promise<Raider | null> {
    return this.raidersRepository.findOne({
      where: { name: name },
      relations: ["characters", "characters.race", "characters.class"],
    });
  }

  public findOneByBattleNetId(battleNetId: string): Promise<Raider | null> {
    return this.raidersRepository.findOneBy({ battleNetId });
  }

  public async create(dto: RaiderCreateDTO): Promise<Raider | null> {
    if (dto.battleNetId === undefined || dto.battleNetId === "") {
      return null;
    }
    const entity = Object.assign(this.raidersRepository.create(), {
      name: dto.name,
      battleTag: dto.battleTag,
      battleNetId: dto.battleNetId,
      guildName: dto.guildName,
    } as Raider);
    return this.raidersRepository.save(entity);
  }

  public async save(raider: Raider): Promise<Raider> {
    return this.raidersRepository.save(raider);
  }
}
