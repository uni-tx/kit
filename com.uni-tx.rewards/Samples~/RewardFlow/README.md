# Reward Flow Sample

Headless demo of reward delivery: a currency reward (`coins_50`) lands in the
entity-based currency system, an item reward (`sword`) lands on the registered
`DemoInventory` entity, and re-granting the same grant id changes nothing.

**Setup**

1. Register `rewards.json` under the `content` label (or update the sample to match your
   content wiring), and make sure the `content` Addressables group is built.
2. The currency reward needs the `coins` currency from the Currency Wallet sample —
   register its `currencies.json` in the same content group.
3. Attach `RewardFlowSample` to any GameObject in a scene that runs the kit bootstrap.
4. Watch the console for the final balances.
