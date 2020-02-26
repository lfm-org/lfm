import {
  Entity,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
  Column,
  OneToOne,
  JoinColumn,
  ManyToOne
} from "typeorm";
import { Race } from "../wow/race.entity";
import { Class } from "../wow/class.entity";
import { Raider } from "../raiders/raider.entity";

@Entity()
export class Character {
  @PrimaryGeneratedColumn()
  id: number;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  updatedTime: Date;

  @Column({ type: "text", nullable: false })
  region: string;

  @Column({ type: "text", nullable: false })
  realm: string;

  @Column({ type: "text", nullable: false })
  name: string;

  @OneToOne(() => Class)
  @JoinColumn()
  class: Class;

  @OneToOne(() => Race)
  @JoinColumn()
  race: Race;

  @Column({ nullable: true })
  level?: number;

  @ManyToOne(
    () => Raider,
    raider => raider.characters
  )
  raider: Raider;
}
