# Shop Screen

A shop wired to the IAP facade end to end: awaiting a purchase, granting content from the
entitlement event, restoring on iOS, and showing store-localized prices.

## Using it

1. Create a catalog via `Assets ▸ Create ▸ UniTx ▸ IAP ▸ Config` and add your product ids.
2. Put `ShopSample` on a GameObject and assign the config.
3. Wire `BuyGems()` and `Restore()` to buttons.

The sample installs `NoOpIapProvider`, so it runs anywhere and every purchase reports
`Unsupported`. Install `com.unity.purchasing` (5.0.0+) and swap in `UnityIapProvider` to
talk to a real store.

## What to copy from it

**Content is granted in `Grant`, subscribed to `UniIap.OnPurchased` — not where the
purchase is awaited.** `PurchaseAsync` returns only for the purchase you started, so
restores, subscription renewals and Ask-to-Buy approvals that clear later never reach it.
Granting from its return value loses purchases the player has already paid for.

The `switch` on `IapResult` shows the other half: react to the *outcome* there — a toast,
a retry prompt — while the entitlement itself is delivered by the event.
