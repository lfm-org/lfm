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

  findAll(): Promise<Character[]> {
    return this.charactersRepository.find();
  }

  findOne(id: number): Promise<Character> {
    return this.charactersRepository.findOne(id);
  }

  async create(character: Character): Promise<void> {
    await this.charactersRepository.save(character);
  }

  async remove(character: Character): Promise<void> {
    await this.charactersRepository.delete(character.id);
  }
}
