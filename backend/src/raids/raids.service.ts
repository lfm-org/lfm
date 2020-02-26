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

  findAll(): Promise<Raid[]> {
    return this.raidsRepository.find({
      relations: ["roster", "bench", "instance"]
    });
  }

  findOne(id: string): Promise<Raid> {
    return this.raidsRepository.findOne(id);
  }

  async create(raid: Raid): Promise<void> {
    await this.raidsRepository.save(raid);
  }

  async remove(raid: Raid): Promise<void> {
    await this.raidsRepository.delete(raid.id);
  }
}
