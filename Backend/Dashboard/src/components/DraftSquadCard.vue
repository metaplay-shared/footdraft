<!-- FOOTDRAFT — player detail card: the manager's drafted XI, league membership and wallet. -->

<template lang="pug">
MCard(title="Drafted XI & League" data-testid="draft-squad-card")
  div(v-if="model")
    div
      strong Formation:
      |  {{ draftFormation }}
    div
      strong Picks:
      |  {{ pickCount }}/11{{ draftLocked ? ' (locked)' : '' }}
    div
      strong League:
      |  {{ leagueLine }}
    div
      strong Wallet:
      |  {{ coins }} Coins · {{ gems }} Gems · {{ shards }} Shards
  span(v-else) Loading player…
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MCard } from '@metaplay/meta-ui-next'
import { getSinglePlayerSubscriptionOptions } from '@metaplay/core'
import { useSubscription } from '@metaplay/subscriptions'

const props = defineProps<{ playerId: string }>()

const { data: playerData } = useSubscription(() => getSinglePlayerSubscriptionOptions(props.playerId))

const model = computed((): any => playerData.value?.model)

const draftFormation = computed((): string => model.value?.draft?.formation || 'none')
const draftLocked = computed((): boolean => model.value?.draft?.locked === true)
const pickCount = computed((): number => Object.keys(model.value?.draft?.picks ?? {}).length)

const leagueLine = computed((): string => {
  const league = model.value?.league
  if (!league?.code) return 'not in a league'
  const snap = league.snapshot
  if (snap && typeof snap.myIndex === 'number' && snap.myIndex >= 0 && Array.isArray(snap.table)) {
    const pos = snap.table.findIndex((r: any) => r.teamIndex === snap.myIndex)
    if (pos >= 0) return `${league.code} — position ${pos + 1}/${snap.table.length}`
  }
  return league.code
})

// Enum dictionary keys serialize as the enum NAMES (verified live payload: {"Coins":250,...}).
const balance = (key: string): number =>
  Number(model.value?.wallet?.balances?.[key] ?? model.value?.wallet?.balances?.[key.toLowerCase()] ?? 0)
const coins = computed((): number => balance('Coins'))
const gems = computed((): number => balance('Gems'))
const shards = computed((): number => balance('Shards'))
</script>
