export interface ReferenceSyncPlanDetail {
  id: number;
  blobName: string;
  href: string;
}

export interface ReferenceSyncPlan {
  indexBlobName: string;
  metaBlobName: string;
  documentCount: number;
  details: ReferenceSyncPlanDetail[];
}

interface CreateReferenceSyncPlanArgs<TIndexResponse> {
  entity: string;
  indexResponse: TIndexResponse;
  getDetails: (response: TIndexResponse) => Array<{
    id: number;
    href: string;
  }>;
}

export function createReferenceSyncPlan<TIndexResponse>(
  args: CreateReferenceSyncPlanArgs<TIndexResponse>
): ReferenceSyncPlan {
  const details = args.getDetails(args.indexResponse);

  return {
    indexBlobName: `reference/${args.entity}/index.json`,
    metaBlobName: `reference/${args.entity}/meta.json`,
    documentCount: details.length,
    details: details.map((detail) => ({
      id: detail.id,
      blobName: `reference/${args.entity}/${detail.id}.json`,
      href: detail.href,
    })),
  };
}
