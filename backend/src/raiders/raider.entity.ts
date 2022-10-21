import {
  Column,
  CreateDateColumn,
  Entity,
  Index,
  OneToMany,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from "typeorm";
import { Character } from "../characters/character.entity";

@Entity()
export class Raider {
  @PrimaryGeneratedColumn()
  public id: number;

  @CreateDateColumn({ name: "created_time", type: "timestamp with time zone" })
  public createdTime: Date;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  public updatedTime: Date;

  @OneToMany(
    () => Character,
    (character) => character.raider
  )
  public characters: Character[];

  @Column({ type: "text", nullable: true })
  public name?: string;

  @Column({
    type: "text",
    name: "google_sub",
    nullable: true,
    select: false,
  })
  @Index({ nullFiltered: true, unique: true })
  public googleSub?: string;
}
