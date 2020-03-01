import { HttpService, Injectable, Logger } from "@nestjs/common";
import { InjectRepository } from "@nestjs/typeorm";
import * as moment from "moment";
import { MoreThan, Repository } from "typeorm";
import { Blizzard } from "./blizzard.entity";
import { Class } from "./class.entity";
import { Instance } from "./instance.entity";
import { Race } from "./race.entity";

@Injectable()
export class WoWService {
  private accessToken: string;
  private readonly baseUrl: string = "https://eu.api.blizzard.com/data/wow/";

  constructor(
    private readonly httpService: HttpService,
    @InjectRepository(Blizzard)
    private readonly blizzardRepository: Repository<Blizzard>,
    @InjectRepository(Class)
    private readonly classesRepository: Repository<Class>,
    @InjectRepository(Race)
    private readonly racesRepository: Repository<Race>,
    @InjectRepository(Instance)
    private readonly instancesRepository: Repository<Instance>
  ) {}

  public async isTimeToUpdate(): Promise<boolean> {
    const response = await this.blizzardRepository.findAndCount({
      select: ["createdTime", "success"],
      where: {
        createdTime: MoreThan(
          moment
            .utc()
            .subtract(1, "days")
            .format()
        ),
        success: true
      }
    });
    return response[1] < 1;
  }

  public async lastUpdated(success: boolean): Promise<void> {
    const entity = Object.assign(this.blizzardRepository.create(), { success });
    await this.blizzardRepository.save(entity);
  }

  public defaultParams() {
    return {
      locale: "en_US"
    };
  }

  public defaultHeaders(bnetns = "static") {
    return {
      Authorization: "Bearer " + this.accessToken,
      "Battlenet-Namespace": bnetns + "-eu"
    };
  }

  public async auth() {
    this.accessToken = null;
    await this.httpService
      .post(
        "https://eu.battle.net/oauth/token",
        "grant_type=client_credentials",
        {
          auth: {
            password: "REDACTED_CLIENT_SECRET",
            username: "REDACTED_CLIENT_ID"
          }
        }
      )
      .toPromise()
      .then(response => this.onAuth(response))
      .catch(error => this.onError(error));
  }

  public async classes() {
    await this.httpService
      .get(this.baseUrl + "playable-class/index", {
        headers: this.defaultHeaders(),
        params: this.defaultParams()
      })
      .toPromise()
      .then(response => {
        response.data.classes.map(classEntry =>
          this.httpService
            .get(this.baseUrl + "playable-class/" + classEntry.id, {
              headers: this.defaultHeaders(),
              params: this.defaultParams()
            })
            .toPromise()
            .then(response2 => this.onClasses(response2))
            .catch(error => this.onError(error))
        );
      })
      .catch(error => this.onError(error));
  }

  public async races() {
    await this.httpService
      .get(this.baseUrl + "playable-race/index", {
        headers: this.defaultHeaders(),
        params: this.defaultParams()
      })
      .toPromise()
      .then(response => {
        response.data.races.map(raceEntry =>
          this.httpService
            .get(this.baseUrl + "playable-race/" + raceEntry.id, {
              headers: this.defaultHeaders(),
              params: this.defaultParams()
            })
            .toPromise()
            .then(response2 => this.onRaces(response2))
            .catch(error => this.onError(error))
        );
      })
      .catch(error => this.onError(error));
  }

  public async instances() {
    await this.httpService
      .get(this.baseUrl + "journal-instance/index", {
        headers: this.defaultHeaders(),
        params: this.defaultParams()
      })
      .toPromise()
      .then(response => {
        response.data.instances.map(instanceEntry =>
          this.httpService
            .get(this.baseUrl + "journal-instance/" + instanceEntry.id, {
              headers: this.defaultHeaders(),
              params: this.defaultParams()
            })
            .toPromise()
            .then(response2 => this.onInstances(response2))
            .catch(error => this.onError(error))
        );
      })
      .catch(error => this.onError(error));
  }

  public onAuth(response) {
    this.accessToken = response.data.access_token;
  }

  public onClasses(response) {
    const newClass = new Class();
    newClass.id = response.data.id;
    newClass.name = response.data.name;
    this.classesRepository.save(newClass);
  }

  public onRaces(response) {
    const newRace = new Race();
    newRace.id = response.data.id;
    newRace.name = response.data.name;
    newRace.faction = response.data.faction.type;
    this.racesRepository.save(newRace);
  }

  public onInstances(response) {
    const newInstance = new Instance();
    newInstance.id = response.data.id;
    newInstance.name = response.data.name;
    newInstance.type = response.data.category.type;
    newInstance.minLevel = response.data.minimum_level || 0;
    newInstance.modes = response.data.modes.map(
      modeEntry => modeEntry.mode.name
    );
    this.instancesRepository.save(newInstance);
  }

  public onError(error) {
    Logger.log(
      JSON.stringify(error.response.statusText) +
        " [" +
        JSON.stringify(error.response.status) +
        "] " +
        JSON.stringify(error.response.data)
    );
  }

  public async race(raceId) {
    return await this.racesRepository.findOne(raceId);
  }

  public async class(classId) {
    return await this.classesRepository.findOne(classId);
  }

  public async instance(instanceId) {
    return await this.instancesRepository.findOne(instanceId);
  }
}
