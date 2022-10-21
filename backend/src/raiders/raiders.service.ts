import { Injectable } from "@nestjs/common";
import { InjectRepository } from "@nestjs/typeorm";
import { Repository } from "typeorm";
import { Raider } from "./raider.entity";

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

  public findOneByNameAuth(name: string): Promise<Raider | null> {
    return this.raidersRepository
      .createQueryBuilder("raider")
      .where("raider.name = :name", { name })
      .select()
      .addSelect("raider.passwordHash")
      .getOne();
  }

  public findOneByGoogleSub(googleSub: string): Promise<Raider | null> {
    return this.raidersRepository.findOneBy({ googleSub: googleSub });
  }

  public async create(dto: RaiderCreateDTO): Promise<Raider | null> {
    // Must contain at least one sub
    if (dto.googleSub === undefined) {
      return null;
    }
    const entity = Object.assign(this.raidersRepository.create(), {
      name: dto.name,
      googleSub: dto.googleSub,
    } as Raider);
    return this.raidersRepository.save(entity);
  }
}
