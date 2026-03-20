export interface ReferenceSyncPlanDetail {
  id: number;
  blobName: string;
  path: string;
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
  getDetailIds: (response: TIndexResponse) => number[];
  getDetailPath: (id: number) => string;
}

export function createReferenceSyncPlan<TIndexResponse>(
  args: CreateReferenceSyncPlanArgs<TIndexResponse>
): ReferenceSyncPlan {
  const ids = args.getDetailIds(args.indexResponse);

  return {
    indexBlobName: `reference/${args.entity}/index.json`,
    metaBlobName: `reference/${args.entity}/meta.json`,
    documentCount: ids.length,
    details: ids.map((id) => ({
      id,
      blobName: `reference/${args.entity}/${id}.json`,
      path: args.getDetailPath(id),
    })),
  };
}
