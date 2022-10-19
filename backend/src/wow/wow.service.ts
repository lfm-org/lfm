import { HttpService, Injectable, Logger } from "@nestjs/common";
import { InjectRepository } from "@nestjs/typeorm";
import * as moment from "moment";
import { MoreThan, Repository } from "typeorm";
import { WoWMeta } from "./meta.entity";
import { WoWClass } from "./class.entity";
import { WoWInstance } from "./instance.entity";
import { WoWRace } from "./race.entity";

@Injectable()
export class WoWService {
  private accessToken: string;
  private readonly baseUrl: string = "https://eu.api.blizzard.com/data/wow/";

  constructor(
    private readonly httpService: HttpService,
    @InjectRepository(WoWMeta)
    private readonly blizzardRepository: Repository<WoWMeta>,
    @InjectRepository(WoWClass)
    private readonly classesRepository: Repository<WoWClass>,
    @InjectRepository(WoWRace)
    private readonly racesRepository: Repository<WoWRace>,
    @InjectRepository(WoWInstance)
    private readonly instancesRepository: Repository<WoWInstance>
  ) {}

  private async isTimeToUpdate(): Promise<boolean> {
    const response = await this.blizzardRepository.findAndCount({
      select: ["createdTime", "success"],
      where: {
        createdTime: MoreThan(
          moment
            .utc()
            .subtract(1, "days")
            .format()
        ),
        success: true,
      },
    });
    return response[1] < 1;
  }

  private async lastUpdated(success: boolean): Promise<void> {
    const entity = Object.assign(this.blizzardRepository.create(), { success });
    await this.blizzardRepository.save(entity);
  }

  private defaultParams() {
    return {
      locale: "en_US",
    };
  }

  private defaultHeaders(bnetns = "static", isClassic = false) {
    return {
      Authorization: `Bearer ${this.accessToken}`,
      "Battlenet-Namespace": `${bnetns}${
        isClassic === true ? "-classic" : ""
      }-eu`,
    };
  }

  private async auth() {
    this.accessToken = null;
    await this.httpService
      .post(
        "https://eu.battle.net/oauth/token",
        "grant_type=client_credentials",
        {
          auth: {
            password: process.env.BLIZZARD_PASSWORD,
            username: process.env.BLIZZARD_USERNAME,
          },
        }
      )
      .toPromise()
      .then((response) => this.onAuth(response))
      .catch((error) => this.onError(error));
  }

  private async classes() {
    await this.httpService
      .get(this.baseUrl + "playable-class/index", {
        headers: this.defaultHeaders(),
        params: this.defaultParams(),
      })
      .toPromise()
      .then((response) => {
        response.data.classes.map((classEntry) =>
          this.httpService
            .get(this.baseUrl + "playable-class/" + classEntry.id, {
              headers: this.defaultHeaders(),
              params: this.defaultParams(),
            })
            .toPromise()
            .then((response2) => this.onClasses(response2))
            .catch((error) => this.onError(error))
        );
      })
      .catch((error) => this.onError(error));
  }

  private async races() {
    await this.httpService
      .get(this.baseUrl + "playable-race/index", {
        headers: this.defaultHeaders(),
        params: this.defaultParams(),
      })
      .toPromise()
      .then((response) => {
        response.data.races.map((raceEntry) =>
          this.httpService
            .get(this.baseUrl + "playable-race/" + raceEntry.id, {
              headers: this.defaultHeaders(),
              params: this.defaultParams(),
            })
            .toPromise()
            .then((response2) => this.onRaces(response2))
            .catch((error) => this.onError(error))
        );
      })
      .catch((error) => this.onError(error));
  }

  private async instances() {
    await this.httpService
      .get(this.baseUrl + "journal-instance/index", {
        headers: this.defaultHeaders(),
        params: this.defaultParams(),
      })
      .toPromise()
      .then((response) => {
        response.data.instances.map((instanceEntry) =>
          this.httpService
            .get(this.baseUrl + "journal-instance/" + instanceEntry.id, {
              headers: this.defaultHeaders(),
              params: this.defaultParams(),
            })
            .toPromise()
            .then((response2) => this.onInstances(response2))
            .catch((error) => this.onError(error))
        );
      })
      .catch((error) => this.onError(error));
  }

  private onAuth(response) {
    this.accessToken = response.data.access_token;
  }

  private onClasses(response) {
    const newClass = new WoWClass();
    newClass.id = response.data.id;
    newClass.name = response.data.name;
    this.classesRepository.save(newClass);
  }

  private onRaces(response) {
    const newRace = new WoWRace();
    newRace.id = response.data.id;
    newRace.name = response.data.name;
    newRace.faction = response.data.faction.type;
    this.racesRepository.save(newRace);
  }

  private onInstances(response) {
    const newInstance = new WoWInstance();
    newInstance.id = response.data.id;
    newInstance.name = response.data.name;
    newInstance.type = response.data.category.type;
    newInstance.minLevel = response.data.minimum_level || 0;
    newInstance.modes = response.data.modes.map(
      (modeEntry) => modeEntry.mode.name
    );
    this.instancesRepository.save(newInstance);
  }

  private onError(error) {
    error.response
      ? Logger.log(
          `${JSON.stringify(error.response.statusText)} [${JSON.stringify(
            error.response.status
          )}] ${JSON.stringify(error.response.data)}`
        )
      : Logger.log(`${JSON.stringify(error)}`);
  }

  public async update(): Promise<void> {
    this.isTimeToUpdate().then((update) => {
      if (update) {
        this.auth()
          .then(() => {
            this.classes();
            this.races();
            this.instances();
            this.lastUpdated(true);
            Logger.log("Blizzard Update Completed.");
          })
          .catch(() => {
            this.lastUpdated(false);
          });
      }
    });
  }
}
