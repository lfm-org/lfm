import { Entity, UpdateDateColumn, Column, PrimaryColumn } from "typeorm";

@Entity()
export class Instance {
  @PrimaryColumn()
  id: number;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  updatedTime: Date;

  @Column({ type: "text", nullable: false })
  name: string;

  @Column({ type: "text", array: true, nullable: false })
  modes: string[];

  @Column({ type: "text", nullable: false })
  type: string;

  @Column({ name: "minimum_level", nullable: false })
  minLevel: number;
}
