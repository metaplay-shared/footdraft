<!-- FOOTDRAFT — LiveOps Dashboard "Season Leagues" page: every league the singleton LeagueActor is running. -->

<template lang="pug">
MViewContainer
  MListCard(
    title="Season Leagues"
    :item-list="leagues"
    :item-key="getKey"
    empty-list-message="No season leagues yet. Create one in the game client and it will appear here."
    permission="api.season_leagues.view"
    :error="error"
    data-testid="season-leagues-card"
    )
    template(#item="{ item: league }")
      MListItem {{ league.name || '(unnamed league)' }}
        template(#top-right) {{ stateLabel(league.state) }}
        template(#bottom-left) {{ subtitle(league) }}
        template(#bottom-right): MTextButton(
          :to="`/seasonLeagues/${league.code}`"
          permission="api.season_leagues.view"
          ) View
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MViewContainer, MListCard, MListItem, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSeasonLeaguesSubscriptionOptions, leagueStateLabel } from '../seasonLeagueSubscriptions'

interface SeasonLeagueListEntry {
  code: string
  name: string | null
  state: number | string
  humanCount: number
  memberCount: number
  currentMatchday: number
  totalMatchdays: number
}

const { data, error } = useSubscription(getSeasonLeaguesSubscriptionOptions())

const leagues = computed((): SeasonLeagueListEntry[] | undefined => data.value?.leagues ?? undefined)

const getKey = (league: SeasonLeagueListEntry): string => league.code
const stateLabel = (state: number | string): string => leagueStateLabel(state)

function subtitle(league: SeasonLeagueListEntry): string {
  const base = `Code ${league.code} · ${league.humanCount} manager(s) of ${league.memberCount} teams`
  return league.totalMatchdays ? `${base} · matchday ${league.currentMatchday}/${league.totalMatchdays}` : base
}
</script>
