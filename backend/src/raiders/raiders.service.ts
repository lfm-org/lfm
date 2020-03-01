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

  public findOneById(id: number): Promise<Raider | undefined> {
    return this.raidersRepository.findOne({ id });
  }

  public findOneByName(name: string): Promise<Raider> {
    return this.raidersRepository.findOne(
      { name },
      { relations: ["characters", "characters.race", "characters.class"] }
    );
  }

  public findOneByNameAuth(name: string): Promise<Partial<Raider>> {
    return this.raidersRepository
      .createQueryBuilder("raider")
      .where("raider.name = :name", { name })
      .select()
      .addSelect("raider.passwordHash")
      .getOne();
  }

  public async create(name: string, passwordHash: string): Promise<Raider> {
    const entity = Object.assign(this.raidersRepository.create(), {
      name,
      passwordHash
    });
    return this.raidersRepository.save(entity);
  }
}
