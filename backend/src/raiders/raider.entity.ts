import {
  Column,
  CreateDateColumn,
  Entity,
  OneToMany,
  PrimaryGeneratedColumn,
  Unique,
  UpdateDateColumn
} from "typeorm";
import { Character } from "../characters/character.entity";

@Entity()
@Unique("unique_name", ["name"])
export class Raider {
  @PrimaryGeneratedColumn()
  public id: number;

  @CreateDateColumn({ name: "created_time", type: "timestamp with time zone" })
  public createdTime: Date;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  public updatedTime: Date;

  @OneToMany(
    () => Character,
    character => character.raider
  )
  public characters: Character[];

  @Column({ type: "text", nullable: false })
  public name: string;

  @Column({
    type: "text",
    // tslint:disable-next-line:object-literal-sort-keys
    name: "password_hash",
    nullable: false,
    select: false
  })
  public passwordHash: string;
}
