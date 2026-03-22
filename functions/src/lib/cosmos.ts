import { CosmosClient, Container } from "@azure/cosmos";
import { DefaultAzureCredential } from "@azure/identity";

let _client: CosmosClient | null = null;

export function createCosmosClientOptions(
  env: Record<string, string | undefined> = process.env
): ConstructorParameters<typeof CosmosClient>[0] {
  const endpoint = env.COSMOS_ENDPOINT;
  if (!endpoint) throw new Error("COSMOS_ENDPOINT environment variable is not set");

  if (endpoint.startsWith("http://")) {
    if (!env.COSMOS_KEY) {
      throw new Error("COSMOS_KEY environment variable is required for HTTP Cosmos endpoints");
    }
    return { endpoint, key: env.COSMOS_KEY };
  }

  if (env.COSMOS_KEY) {
    return { endpoint, key: env.COSMOS_KEY };
  }

  return { endpoint, aadCredentials: new DefaultAzureCredential() };
}

function getClient(): CosmosClient {
  if (!_client) {
    _client = new CosmosClient(createCosmosClientOptions());
  }
  return _client;
}

export function getRaidersContainer(): Container {
  return getClient().database(process.env.COSMOS_DATABASE!).container("raiders");
}

export function getRaidsContainer(): Container {
  return getClient().database(process.env.COSMOS_DATABASE!).container("raids");
}
