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
  private readonly region: WoWHeaderOptionsRegion = WoWHeaderOptionsRegion.EU;
  private readonly baseUrls: { [key in WoWHeaderOptionsRegion]: string } = {
    [WoWHeaderOptionsRegion.US]: "https://us.api.blizzard.com",
    [WoWHeaderOptionsRegion.EU]: "https://eu.api.blizzard.com",
    [WoWHeaderOptionsRegion.KR]: "https://kr.api.blizzard.com",
    [WoWHeaderOptionsRegion.TW]: "https://tw.api.blizzard.com",
    [WoWHeaderOptionsRegion.CN]: "https://gateway.battlenet.com.cn",
  };
  private readonly baseUrl: string = `${this.baseUrls[this.region]}/data/wow`;
  private readonly rateLimitDelay: number = 20;
  private accessToken: string;

  constructor(
    private readonly httpService: HttpService,
    @InjectRepository(WoWMeta)
    private readonly metaRepository: Repository<WoWMeta>,
    @InjectRepository(WoWClass)
    private readonly classesRepository: Repository<WoWClass>,
    @InjectRepository(WoWRace)
    private readonly racesRepository: Repository<WoWRace>,
    @InjectRepository(WoWInstance)
    private readonly instancesRepository: Repository<WoWInstance>
  ) {}

  private async isTimeToUpdate(): Promise<boolean> {
    const response = await this.metaRepository.findAndCount({
      select: ["createdTime", "success"],
      where: {
        createdTime: MoreThan(
          moment
            .utc()
            .subtract(1, "months")
            .format()
        ),
        success: true,
      },
    });
    return response[1] < 1;
  }

  private async lastUpdated(success: boolean): Promise<void> {
    const entity = Object.assign(this.metaRepository.create(), { success });
    await this.metaRepository.save(entity);
  }

  private headers(options: WoWHeaderOptions) {
    return {
      Authorization: `Bearer ${this.accessToken}`,
      "Battlenet-Namespace": `${options.namespaceCategory}${
        options.classic === true ? "-classic" : ""
      }-${options.region}`,
    };
  }

  private auth(onComplete: () => void, onError: () => void) {
    this.accessToken = null;
    this.httpService
      .post<WoWAuth>(
        "https://eu.battle.net/oauth/token",
        "grant_type=client_credentials",
        {
          auth: {
            password: process.env.BLIZZARD_PASSWORD,
            username: process.env.BLIZZARD_USERNAME,
          },
        }
      )
      .subscribe(
        (response) => {
          this.onAuth(response.data);
        },
        (error) => {
          onError();
          this.onError("auth", error);
        },
        () => {
          onComplete();
        }
      );
  }

  private async updateClasses() {
    const headers = this.headers({
      region: this.region,
      namespaceCategory: WoWHeaderOptionsNamespaceCategory.STATIC,
      classic: true,
    });
    this.httpService
      .get<WoWPlayableClassIndex>(`${this.baseUrl}/playable-class/index`, {
        headers: headers,
      })
      .subscribe(
        (indexResponse) => {
          indexResponse.data.classes.forEach(async (entry, index) => {
            await this.sleep(index * this.rateLimitDelay);
            this.httpService
              .get<WoWPlayableClass>(entry.key.href, {
                headers: headers,
              })
              .subscribe(
                (entryResponse) => {
                  this.onClass(entryResponse.data);
                },
                (error) => {
                  this.onError("playable class", error);
                }
              );
          });
        },
        (error) => {
          this.onError("playable classes index", error);
        }
      );
  }

  private async updateRaces() {
    const headers = this.headers({
      region: this.region,
      namespaceCategory: WoWHeaderOptionsNamespaceCategory.STATIC,
      classic: true,
    });
    this.httpService
      .get<WoWPlayableRaceIndex>(`${this.baseUrl}/playable-race/index`, {
        headers: headers,
      })
      .subscribe(
        (indexResponse) => {
          indexResponse.data.races.forEach(async (entry, index) => {
            await this.sleep(index * this.rateLimitDelay);
            this.httpService
              .get<WoWPlayableRace>(entry.key.href, {
                headers: headers,
              })
              .subscribe(
                (entryResponse) => {
                  this.onRace(entryResponse.data);
                },
                (error) => {
                  this.onError("playable race", error);
                }
              );
          });
        },
        (error) => {
          this.onError("playable races index", error);
        }
      );
  }

  private async updateInstances() {
    const headers = this.headers({
      region: this.region,
      namespaceCategory: WoWHeaderOptionsNamespaceCategory.STATIC,
      classic: false, // classic doesn't have instances api, so fetching from retail api
    });
    this.httpService
      .get<WoWJournalInstanceIndex>(`${this.baseUrl}/journal-instance/index`, {
        headers: headers,
      })
      .subscribe(
        (indexResponse) => {
          indexResponse.data.instances.forEach(async (entry, index) => {
            await this.sleep(index * this.rateLimitDelay);
            this.httpService
              .get(entry.key.href, {
                headers: headers,
              })
              .subscribe(
                (entryResponse) => {
                  this.onInstance(entryResponse.data);
                },
                (error) => {
                  this.onError("instance", error);
                }
              );
          });
        },
        (error) => {
          this.onError("instances index", error);
        }
      );
  }

  private sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  private onAuth(auth: WoWAuth) {
    this.accessToken = auth.access_token;
  }

  private onClass(playableClass: WoWPlayableClass) {
    const newClass = new WoWClass(playableClass);
    this.classesRepository.save(newClass);
  }

  private onRace(playableRace: WoWPlayableRace) {
    const newRace = new WoWRace(playableRace);
    this.racesRepository.save(newRace);
  }

  private onInstance(instance: WoWJournalInstance) {
    const newInstance = new WoWInstance(instance);
    this.instancesRepository.save(newInstance);
  }

  private onError(context: string, error) {
    error.response
      ? Logger.log(
          `[${context}] ${JSON.stringify(
            error.response.statusText
          )} [${JSON.stringify(error.response.status)}] ${JSON.stringify(
            error.response.data
          )}`
        )
      : Logger.log(`${JSON.stringify(error)}`);
  }

  public async update(): Promise<void> {
    this.isTimeToUpdate().then((update) => {
      if (update) {
        this.auth(
          () => {
            this.updateClasses();
            this.updateRaces();
            this.updateInstances();
            // this.lastUpdated(true);
            Logger.debug("WoW API data updated");
          },
          () => {
            this.lastUpdated(false);
          }
        );
      }
    });
  }
}
