# Quests Screen

A uGUI quest list: one row per quest with the quest icon, name and description, one
progress line per objective, a claim button that appears on completion, and locked/claimed
overlays.

## What it demonstrates

- Binding a screen to the quest service through the `UniQuests` static facade, repainting
  from `OnChanged` instead of polling in `Update`.
- One `QuestRowCell` per quest, each reading the snapshot's per-quest state:
  - **Available / In progress** — progress lines with `current/target`.
  - **Completed** — the claim button appears.
  - **Locked** — dimmed, showing the prerequisite gate rather than hiding the quest.
  - **Claimed** — a claimed overlay; the row stays visible so the reward is not forgotten.
- Claiming from a row and handling a refusal (`QuestClaimResult.GrantFailed`) the same way
  the daily-rewards screen does: the reward is still owed and will be retried on the next
  refresh.

## Setup

1. Import the `Quests Flow` sample too, so `quests_default.json` exists (this screen has
   no content of its own — it renders whatever set is registered).
2. Tag `quests_default.json` with an Addressables label and register the set exactly as the
   flow sample does (`ContentRegistry.Register<QuestSetData>("quests_default")`), or bind
   your own content.
3. Point the screen's fields at your scene:
   - `Set Name Label` — a `Text` for the set name.
   - `Countdown Label` — a `Text` for the time until the next reset.
   - `Row Prefab` — a `QuestRowCell` prefab with its own fields wired.
   - `List Content` — a `RectTransform` (a `VerticalLayoutGroup` works well) to instantiate
     rows into.

## Notes

- The sample's assembly is compiled only when `com.uni-tx.widgets` and
  `com.uni-tx.sprite-loader` are installed (see the asmdef's `versionDefines`); without
  them it is skipped rather than breaking the project.
- Icons load through `ImageSpriteLoader` from the quest's `_iconAddress`. Leave the
  address empty to skip loading.
