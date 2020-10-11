import { Injectable } from "@nestjs/common";
import { InjectRepository } from "@nestjs/typeorm";
import { Repository } from "typeorm";
import { Raid } from "./raid.entity";

@Injectable()
export class RaidsService {
  constructor(
    @InjectRepository(Raid)
    private readonly raidsRepository: Repository<Raid>
  ) {}

  public findAll(): Promise<Raid[]> {
    return this.raidsRepository.find({
      relations: ["instance"]
    });
  }

  public findOne(id: number): Promise<Raid> {
    return this.raidsRepository.findOne(id, {
      relations: ["raidCharacters", "raidCharacters.character", "instance"]
    });
  }

  public async create(raid: Raid): Promise<void> {
    const entity = Object.assign(this.raidsRepository.create(), raid);
    await this.raidsRepository.save(entity);
  }

  public async remove(raid: Raid): Promise<void> {
    await this.raidsRepository.delete(raid.id);
  }
}
