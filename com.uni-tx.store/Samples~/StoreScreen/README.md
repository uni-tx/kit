# Store Screen sample

A ready-to-restyle uGUI shop: a header with the store name, one scrollable feed of offers
grouped under their section headers, and a claim / buy / watch button per row.

## What it demonstrates

- **Binding to `UniStore.OnChanged`** rather than polling — the shop changes a handful of
  times per session.
- **Which state each control reads**: `StoreOfferSnapshot.State` drives the action button,
  the cooldown countdown and the claimed overlay.
- **Reading the price from the store**: an IAP row shows `UniIap.GetPrice(productId)`,
  never a hard-coded price — the store returns the player's own currency and formatting,
  and showing a price that differs from the payment sheet is a store-review rejection.
- **What to do with a refusal**: `AdNotCompleted` and `AdNotReady` are logged distinctly
  from a `GrantFailed` (still owed, retried on refresh).

## Wiring

1. Add this folder to the scene.
2. Hook up the prefab's references in the inspector:
   - `_storeNameLabel` — header.
   - `_offerPrefab` — a `StoreOfferCell` row; `_listContent` — the list's content `RectTransform`.
3. The screen binds to `UniStore.Service` on `Initialize()`, so it works with any game
   that installs the service through the bootstrap `StoreStep` or the facade directly.

## Dependencies

The assembly is skipped when the kit's `com.uni-tx.widgets` and `com.uni-tx.sprite-loader`
packages are absent, so installing this sample never breaks a project that does not use
them.
