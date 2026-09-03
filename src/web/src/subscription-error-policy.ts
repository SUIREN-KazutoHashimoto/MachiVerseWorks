export const SUBSCRIPTION_TOO_LARGE_DETAIL_CODE = 'subscriptionVolumeTooLarge';
export const ROAD_SNAPSHOT_TOO_LARGE_DETAIL_CODE = 'roadSnapshotTooLarge';
export const RAILWAY_OPERATIONS_SNAPSHOT_TOO_LARGE_DETAIL_CODE = 'railwayOperationsSnapshotTooLarge';
export const MULTIMODAL_TRANSIT_SNAPSHOT_TOO_LARGE_DETAIL_CODE = 'multimodalTransitSnapshotTooLarge';

export function isRetryableSubscriptionDetailCode(detailCode: string | undefined): boolean {
  return detailCode === SUBSCRIPTION_TOO_LARGE_DETAIL_CODE
    || detailCode === ROAD_SNAPSHOT_TOO_LARGE_DETAIL_CODE
    || detailCode === RAILWAY_OPERATIONS_SNAPSHOT_TOO_LARGE_DETAIL_CODE
    || detailCode === MULTIMODAL_TRANSIT_SNAPSHOT_TOO_LARGE_DETAIL_CODE;
}
