# Economy Screen

A uGUI wallet that shows what the N-economy design looks like from the player's side:
one tab per economy, each listing its currencies with balances, an exchange button where
a rule exists, and a buy button per purchase.

## What it demonstrates

- **One tab per economy** — `EconomyScreenSample` builds the tab strip from
  `IEconomyService.GetEconomyIds()` and swaps the row list when a tab is tapped. Each
  economy keeps its own ledgers; the tabs never mix.
- **Balances from the wallet** — rows read `GetSnapshot(economyId)` which pulls balances
  through the currency service, so the same numbers the rest of the game sees.
- **Exchange** — a row with an exchange rule spending its currency gets an exchange
  button; tapping it converts a fixed amount at the content-defined rate.
- **Buy** — a row whose currency appears in a purchase's costs gets a buy button; tapping
  it charges the costs and grants the rewards through the reward service.

## Setup

1. Import the package's samples via the Package Manager (Windows ▸ Package Manager ▸
   UniTx Economy ▸ Samples ▸ Economy Screen ▸ Import).
2. Requires `com.uni-tx.widgets` and `com.uni-tx.sprite-loader` in the project. The
   sample's assembly is guarded: without them it is skipped rather than breaking the
   project.
3. Wire the screen to the economy service — `BindAsync(service)` — once the kit is
   bootstrapped and content is loaded.

Restyle it, do not extend it. The value here is the wiring: which state each control
reads, and what it does with a refusal.
