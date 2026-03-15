import { CosmosClient, Container } from "@azure/cosmos";
import { DefaultAzureCredential } from "@azure/identity";

let _client: CosmosClient | null = null;

function getClient(): CosmosClient {
  if (!_client) {
    const endpoint = process.env.COSMOS_ENDPOINT;
    if (!endpoint) throw new Error("COSMOS_ENDPOINT environment variable is not set");
    // @azure/cosmos v4+: use `credential`; v3: use `aadCredentials`
    _client = new CosmosClient({ endpoint, credential: new DefaultAzureCredential() });
  }
  return _client;
}

export function getRaidersContainer(): Container {
  return getClient().database("sisu-raidcal").container("raiders");
}

export function getRaidsContainer(): Container {
  return getClient().database("sisu-raidcal").container("raids");
}
