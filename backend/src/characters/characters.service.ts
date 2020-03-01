import { Injectable } from "@nestjs/common";
import { InjectRepository } from "@nestjs/typeorm";
import { Repository } from "typeorm";
import { Character } from "./character.entity";

@Injectable()
export class CharactersService {
  constructor(
    @InjectRepository(Character)
    private readonly charactersRepository: Repository<Character>
  ) {}

  public findAll(): Promise<Character[]> {
    return this.charactersRepository.find();
  }

  public findOne(id: number): Promise<Character> {
    return this.charactersRepository.findOne(id);
  }

  public async create(character: Character): Promise<void> {
    const entity = Object.assign(this.charactersRepository.create(), character);
    await this.charactersRepository.save(entity);
  }

  public async remove(character: Character): Promise<void> {
    await this.charactersRepository.delete(character.id);
  }
}
