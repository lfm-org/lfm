import { Container } from "@azure/cosmos";
import type { UmzugStorage } from "umzug";

const DOCUMENT_ID = "migrations";

interface MigrationsDocument {
  id: string;
  names: string[];
  _etag?: string;
}

export class CosmosMigrationsStorage implements UmzugStorage {
  constructor(private readonly container: Container) {}

  async logMigration({ name }: { name: string }): Promise<void> {
    const doc = await this.readDocument();
    if (!doc.names.includes(name)) {
      doc.names.push(name);
    }
    await this.writeDocument(doc);
  }

  async unlogMigration({ name }: { name: string }): Promise<void> {
    const doc = await this.readDocument();
    doc.names = doc.names.filter((n) => n !== name);
    await this.writeDocument(doc);
  }

  async executed(): Promise<string[]> {
    const doc = await this.readDocument();
    return doc.names;
  }

  private async readDocument(): Promise<MigrationsDocument> {
    const { resource } = await this.container
      .item(DOCUMENT_ID, DOCUMENT_ID)
      .read<MigrationsDocument>();
    return resource ?? { id: DOCUMENT_ID, names: [] };
  }

  private async writeDocument(doc: MigrationsDocument): Promise<void> {
    const options = doc._etag ? { accessCondition: { type: "IfMatch", condition: doc._etag } } : {};
    await this.container.items.upsert(doc, options);
  }
}
