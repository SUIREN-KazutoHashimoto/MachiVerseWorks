export const SUBSCRIPTION_TOO_LARGE_DETAIL_CODE = 'subscriptionVolumeTooLarge';
export const ROAD_SNAPSHOT_TOO_LARGE_DETAIL_CODE = 'roadSnapshotTooLarge';

export function isRetryableSubscriptionDetailCode(detailCode: string | undefined): boolean {
  return detailCode === SUBSCRIPTION_TOO_LARGE_DETAIL_CODE || detailCode === ROAD_SNAPSHOT_TOO_LARGE_DETAIL_CODE;
}
