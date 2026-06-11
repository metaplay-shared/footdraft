// FOOTDRAFT — dashboard subscription options for the Season Leagues admin API (polled every 5s).

import {
  type SubscriptionOptions,
  getFetcherPolicyGet,
  getCacheRetentionPolicyKeepForever,
  getPollingPolicyTimer,
} from '@metaplay/subscriptions'

/** All leagues in the registry. */
export function getSeasonLeaguesSubscriptionOptions(): SubscriptionOptions {
  return {
    permission: 'api.season_leagues.view',
    pollingPolicy: getPollingPolicyTimer(5000),
    fetcherPolicy: getFetcherPolicyGet('/seasonLeagues'),
    cacheRetentionPolicy: getCacheRetentionPolicyKeepForever(),
  }
}

/** A single league's admin snapshot. */
export function getSeasonLeagueSubscriptionOptions(code: string): SubscriptionOptions {
  return {
    permission: 'api.season_leagues.view',
    pollingPolicy: getPollingPolicyTimer(5000),
    fetcherPolicy: getFetcherPolicyGet(`/seasonLeagues/${code}`),
    cacheRetentionPolicy: getCacheRetentionPolicyKeepForever(),
  }
}

/** Map the LeagueState enum integer to a label. */
export function leagueStateLabel(state: number | string): string {
  const labels = ['Lobby', 'Active', 'Finished', 'Drafting']
  return typeof state === 'number' ? (labels[state] ?? String(state)) : String(state)
}
