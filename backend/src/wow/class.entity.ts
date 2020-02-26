import { Entity, UpdateDateColumn, Column, PrimaryColumn } from "typeorm";

@Entity()
export class Class {
  @PrimaryColumn()
  id: number;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  updatedTime: Date;

  @Column({ type: "text", nullable: false })
  name: string;
}
