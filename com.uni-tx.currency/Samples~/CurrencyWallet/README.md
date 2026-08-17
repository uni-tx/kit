# Currency Wallet Sample

Headless demo of the currency system: two currencies built as entities from
`currencies.json`, then granted, re-granted idempotently and spent through
`CurrencyService`.

**Setup**

1. Register `currencies.json` under the `content` label (or update the sample to match
   your content wiring), and make sure the `content` Addressables group is built.
2. Attach `CurrencyWalletSample` to any GameObject in a scene that runs the kit bootstrap.
3. Watch the console. Right-click the component in the inspector for the two context
   menu actions.
