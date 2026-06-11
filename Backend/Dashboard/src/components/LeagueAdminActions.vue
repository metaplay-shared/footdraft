<!-- FOOTDRAFT — LiveOps write-controls for one season league: advance a matchday, open/close the transfer window. -->

<template lang="pug">
MCard(title="Admin controls" data-testid="league-admin-actions")
  div(class="tw-flex tw-flex-wrap tw-gap-2")
    MButton(permission="api.season_leagues.admin" :disabled="busy" @click="advance") Advance matchday
    MButton(permission="api.season_leagues.admin" variant="success" :disabled="busy" @click="setWindow(1)") Open transfer window
    MButton(permission="api.season_leagues.admin" variant="warning" :disabled="busy" @click="setWindow(2)") Close transfer window
    MButton(permission="api.season_leagues.admin" variant="neutral" :disabled="busy" @click="setWindow(0)") Window: follow schedule
</template>

<script lang="ts" setup>
import { ref } from 'vue'

import { MCard, MButton, useNotifications } from '@metaplay/meta-ui-next'
import { useGameServerApi } from '@metaplay/game-server-api'

const props = defineProps<{ code: string }>()

const gameServerApi = useGameServerApi()
const { showSuccessNotification, showErrorNotification } = useNotifications()
const busy = ref(false)

async function advance(): Promise<void> {
  busy.value = true
  try {
    await gameServerApi.post(`/seasonLeagues/${props.code}/advance`)
    showSuccessNotification('Matchday advanced.')
  } catch (e: any) {
    showErrorNotification(e?.response?.data ?? 'Could not advance the matchday.')
  } finally {
    busy.value = false
  }
}

async function setWindow(override: number): Promise<void> {
  busy.value = true
  try {
    await gameServerApi.post(`/seasonLeagues/${props.code}/transferWindow`, { override })
    showSuccessNotification('Transfer window updated.')
  } catch (e: any) {
    showErrorNotification('Could not update the transfer window.')
  } finally {
    busy.value = false
  }
}
</script>
