import {
  Column,
  CreateDateColumn,
  Entity,
  JoinTable,
  ManyToMany,
  ManyToOne,
  PrimaryGeneratedColumn,
  UpdateDateColumn
} from "typeorm";
import { Character } from "../characters/character.entity";
import { Instance } from "../wow/instance.entity";

@Entity()
export class Raid {
  @PrimaryGeneratedColumn()
  public id: number;

  @CreateDateColumn({ name: "start_time", type: "timestamp with time zone" })
  public startTime: Date;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  public updatedTime: Date;

  @Column({ name: "signup_close_time", type: "timestamp with time zone" })
  public signupCloseTime: Date;

  @Column({ name: "description", type: "text", default: null })
  public description: string;

  @Column({ name: "mode", type: "text", default: null })
  public mode: string;

  @ManyToOne(
    () => Instance,
    instance => instance.raids
  )
  public instance: Instance;

  @ManyToMany(() => Character)
  @JoinTable()
  public roster: Character[];

  @ManyToMany(() => Character)
  @JoinTable()
  public bench: Character[];
}
