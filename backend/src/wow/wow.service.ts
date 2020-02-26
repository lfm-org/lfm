import { Injectable, HttpService, Logger } from "@nestjs/common";
import { Class } from "./class.entity";
import { Repository } from "typeorm";
import { InjectRepository } from "@nestjs/typeorm";
import { Race } from "./race.entity";
import { Instance } from "./instance.entity";

@Injectable()
export class WoWService {
  private accessToken: string;
  private readonly baseUrl: string = "https://eu.api.blizzard.com/data/wow/";

  constructor(
    private readonly httpService: HttpService,
    @InjectRepository(Class)
    private readonly classesRepository: Repository<Class>,
    @InjectRepository(Race)
    private readonly racesRepository: Repository<Race>,
    @InjectRepository(Instance)
    private readonly instancesRepository: Repository<Instance>
  ) {}

  defaultParams() {
    return {
      locale: "en_US"
    };
  }

  defaultHeaders(bnetns = "static") {
    return {
      Authorization: "Bearer " + this.accessToken,
      "Battlenet-Namespace": bnetns + "-eu"
    };
  }

  async auth() {
    this.accessToken = null;
    await this.httpService
      .post(
        "https://eu.battle.net/oauth/token",
        "grant_type=client_credentials",
        {
          auth: {
            username: "REDACTED_CLIENT_ID",
            password: "REDACTED_CLIENT_SECRET"
          }
        }
      )
      .toPromise()
      .then(response => this.onAuth(response))
      .catch(error => this.onError(error));
  }

  async classes() {
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
            .then(response => this.onClasses(response))
            .catch(error => this.onError(error))
        );
      })
      .catch(error => this.onError(error));
  }

  async races() {
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
            .then(response => this.onRaces(response))
            .catch(error => this.onError(error))
        );
      })
      .catch(error => this.onError(error));
  }

  async instances() {
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
            .then(response => this.onInstances(response))
            .catch(error => this.onError(error))
        );
      })
      .catch(error => this.onError(error));
  }

  onAuth(response) {
    this.accessToken = response.data.access_token;
  }

  onClasses(response) {
    const newClass = new Class();
    newClass.id = response.data.id;
    newClass.name = response.data.name;
    this.classesRepository.save(newClass);
  }

  onRaces(response) {
    const newRace = new Race();
    newRace.id = response.data.id;
    newRace.name = response.data.name;
    newRace.faction = response.data.faction.type;
    this.racesRepository.save(newRace);
  }

  onInstances(response) {
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

  onError(error) {
    Logger.log(
      JSON.stringify(error.response.statusText) +
        " [" +
        JSON.stringify(error.response.status) +
        "] " +
        JSON.stringify(error.response.data)
    );
  }

  async race(raceId) {
    return await this.racesRepository.findOne(raceId);
  }

  async class(classId) {
    return await this.classesRepository.findOne(classId);
  }

  async instance(instanceId) {
    return await this.instancesRepository.findOne(instanceId);
  }
}
