<!-- FOOTDRAFT — Season League detail: standings, members and the latest matchday for one league. -->

<template lang="pug">
MViewContainer(:is-loading="!snapshot" :error="error")
  template(#overview)
    MPageOverviewCard(:title="snapshot?.name || code")
      | {{ headerLine }}

  LeagueAdminActions(:code="code")

  MCard(title="Standings" data-testid="standings-card")
    table(v-if="table.length" class="tw-w-full tw-text-sm")
      thead
        tr(class="tw-text-left tw-border-b")
          th #
          th Team
          th(class="tw-text-right") P
          th(class="tw-text-right") W
          th(class="tw-text-right") D
          th(class="tw-text-right") L
          th(class="tw-text-right") GF
          th(class="tw-text-right") GA
          th(class="tw-text-right") GD
          th(class="tw-text-right") Pts
      tbody
        tr(v-for="(row, i) in table" :key="row.teamIndex" class="tw-border-b")
          td {{ i + 1 }}
          td {{ nameFor(row.teamIndex) }}
          td(class="tw-text-right") {{ row.played }}
          td(class="tw-text-right") {{ row.won }}
          td(class="tw-text-right") {{ row.drawn }}
          td(class="tw-text-right") {{ row.lost }}
          td(class="tw-text-right") {{ row.goalsFor }}
          td(class="tw-text-right") {{ row.goalsAgainst }}
          td(class="tw-text-right") {{ row.goalsFor - row.goalsAgainst }}
          td(class="tw-text-right tw-font-semibold") {{ row.won * 3 + row.drawn }}
    span(v-else) Standings appear once the season is running.

  MCard(title="Latest matchday" data-testid="matchday-card")
    div(v-if="lastLines.length")
      div(v-for="(line, i) in lastLines" :key="i") {{ line }}
    span(v-else) No matchday has been played yet.

  MCard(title="Managers" data-testid="members-card")
    div(v-for="m in members" :key="m.index")
      | {{ m.crest }} {{ m.name }} — {{ m.formationName || 'no formation' }} · {{ m.picksCount }}/11{{ m.isBot ? ' (CPU)' : '' }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MViewContainer, MPageOverviewCard, MCard } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSeasonLeagueSubscriptionOptions, leagueStateLabel } from '../seasonLeagueSubscriptions'
import LeagueAdminActions from './LeagueAdminActions.vue'

const route = useRoute()
const code = String(route.params.code)

const { data: snapshot, error } = useSubscription(getSeasonLeagueSubscriptionOptions(code))

const table = computed((): any[] => snapshot.value?.table ?? [])
const members = computed((): any[] => snapshot.value?.members ?? [])
const lastLines = computed((): string[] => snapshot.value?.lastMatchdayLines ?? [])

const headerLine = computed((): string => {
  const s = snapshot.value
  if (!s) return ''
  const state = leagueStateLabel(s.state)
  const md = s.totalMatchdays ? ` · matchday ${s.currentMatchday}/${s.totalMatchdays}` : ''
  const tw = s.transferWindowOpen ? ' · 🔁 transfer window OPEN' : ''
  return `${state} · code ${s.code}${md}${tw}`
})

function nameFor(teamIndex: number): string {
  const m = members.value.find((x) => x.index === teamIndex)
  return m ? `${m.crest ?? ''} ${m.name}`.trim() : `Team ${teamIndex}`
}
</script>
