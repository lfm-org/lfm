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

  findOneById(id: number): Promise<Raider | undefined> {
    return this.raidersRepository.findOne({ id: id });
  }

  findOneByName(name: string): Promise<Raider> {
    return this.raidersRepository.findOne(
      { name: name },
      { relations: ["characters", "characters.race", "characters.class"] }
    );
  }

  findOneByNameAuth(name: string): Promise<Partial<Raider>> {
    return this.raidersRepository
      .createQueryBuilder("raider")
      .where("raider.name = :name", { name: name })
      .select()
      .addSelect("raider.passwordHash")
      .getOne();
  }

  create(name: string, passwordHash: string): Promise<Raider> {
    const raider = new Raider();
    raider.name = name;
    raider.passwordHash = passwordHash;
    return this.raidersRepository.save(raider);
  }
}
