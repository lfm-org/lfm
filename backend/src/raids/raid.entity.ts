import {
  Entity,
  Column,
  PrimaryGeneratedColumn,
  CreateDateColumn,
  UpdateDateColumn,
  JoinTable,
  ManyToMany,
  OneToOne,
  JoinColumn
} from "typeorm";
import { Instance } from "../wow/instance.entity";
import { Character } from "../characters/character.entity";

@Entity()
export class Raid {
  @PrimaryGeneratedColumn()
  id: number;

  @CreateDateColumn({ name: "start_time", type: "timestamp with time zone" })
  startTime: Date;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  updatedTime: Date;

  @Column({ name: "signup_close_time", type: "timestamp with time zone" })
  signupCloseTime: Date;

  @Column({ name: "description", type: "text", default: null })
  description: string;

  @OneToOne(() => Instance)
  @JoinColumn()
  instance: Instance;

  @ManyToMany(() => Character)
  @JoinTable()
  roster: Character[];

  @ManyToMany(() => Character)
  @JoinTable()
  bench: Character[];
}
