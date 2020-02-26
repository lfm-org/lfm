import {
  Entity,
  PrimaryGeneratedColumn,
  CreateDateColumn,
  UpdateDateColumn,
  OneToMany,
  Column
} from "typeorm";
import { Character } from "../characters/character.entity";

@Entity()
export class Raider {
  @PrimaryGeneratedColumn()
  id: number;

  @CreateDateColumn({ name: "created_time", type: "timestamp with time zone" })
  createdTime: Date;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  updatedTime: Date;

  @OneToMany(
    () => Character,
    character => character.raider
  )
  characters: Character[];

  @Column({ type: "text", nullable: false })
  name: string;

  @Column({
    type: "text",
    name: "password_hash",
    nullable: false,
    select: false
  })
  passwordHash: string;
}
