// FOOTDRAFT — game-specific LiveOps Dashboard customizations.
//
// Registers the "Season Leagues" page (list + detail), a drafted-XI/league/wallet card on the player detail
// page, and the three player currencies on the overview card.

import type { App } from 'vue'

import { setGameSpecificInitialization } from '@metaplay/core'

/**
 * Vue 3 plugin called after the SDK CorePlugin is registered but before the app is mounted.
 */
export function GameSpecificPlugin(app: App): void {
  setGameSpecificInitialization(async (initializationApi) => {
    // --- "Season Leagues" page (list of all active leagues) ---
    initializationApi.addNavigationEntry(
      {
        path: '/seasonLeagues',
        name: 'Season Leagues',
        component: async () => await import('./components/SeasonLeaguesListView.vue'),
      },
      {
        icon: 'trophy',
        sidebarTitle: 'Season Leagues',
        sidebarOrder: 20,
        category: 'Game',
        permission: 'api.season_leagues.view',
      }
    )

    // --- League detail (route-only; reached from the list, not shown in the sidebar) ---
    initializationApi.addNavigationEntry(
      {
        path: '/seasonLeagues/:code',
        name: 'Season League Detail',
        component: async () => await import('./components/SeasonLeagueDetailView.vue'),
      },
      {
        permission: 'api.season_leagues.view',
      }
    )

    // --- Player detail: drafted XI + league membership + wallet ---
    initializationApi.addUiComponent('Players/Details/Tab0', {
      uniqueId: 'DraftSquadCard',
      vueComponent: async () => await import('./components/DraftSquadCard.vue'),
      width: 'full',
    })

    // --- Currencies on the player overview card ---
    // PlayerWallet.Balances is a MetaDictionary<CurrencyType,long>; enum dictionary KEYS serialize as the enum
    // NAMES — verified against the live /api/players/{id} payload: {"Coins":250,"Gems":50,"Shards":12}.
    const balance = (m: any, key: string): number =>
      Number(m?.wallet?.balances?.[key] ?? m?.wallet?.balances?.[key.toLowerCase()] ?? 0)
    initializationApi.addPlayerResources([
      { displayName: 'Coins', getAmount: (m: any): number => balance(m, 'Coins') },
      { displayName: 'Gems', getAmount: (m: any): number => balance(m, 'Gems') },
      { displayName: 'Shards', getAmount: (m: any): number => balance(m, 'Shards') },
    ])
  })
}
