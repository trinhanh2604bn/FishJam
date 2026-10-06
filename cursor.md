# CURSOR.MD — Fish Jam Fever–Style Mobile Puzzle
## Production-Ready Gameplay Specification v2
## Multi-Tank Target Routing + Five-Slot Waiting Tray + Bubble Pile Gravity

> **Status:** LOGIC LOCK / READY FOR ASSET SPECIFICATION
>
> **Target:** Unity 2D Mobile, portrait.
>
> **Primary goal:** build a playable puzzle with interaction structure, layout hierarchy, timing, motion language, and game feel closely matching the supplied reference captures while using our own code and original/recreated project assets.
>
> **Cursor rule:** This document is the authoritative product/gameplay specification. Do not silently change a rule to make implementation easier. If code conflicts with this document, update the code. If two rules appear contradictory, stop and report the conflict before implementing a different interpretation.

---

# 0. SOURCE-OF-TRUTH PRIORITY

When deciding behavior, use this order:

```text
1. Latest explicit rule approved by the user
2. This CURSOR.MD
3. Supplied reference screenshots/video
4. Existing project code
5. Cursor assumptions
```

Never change business rules based only on assumptions.

---

# 1. CURRENT LOCKED PRODUCT RULES

```text
MAX_TANK_SLOTS                  = 4
DEFAULT_UNLOCKED_TANKS          = 2
TANK_CAPACITY                   = 3

WAITING_TRAY_SLOTS              = 5
WAITING_TRAY_FAIL_COUNT         = 5

LEVEL_COMPLETE_GOLD_REWARD      = 20
LOSE_LIFE_COST                  = 1

EXTRA_TANK_GOLD_UNLOCK_COST     = 600
EXTRA_TANK_AD_UNLOCK_ALLOWED    = true

FAIL_TIMER                      = NONE

DISTINCT_FISH_TYPES_PER_BUBBLE  = 3
MAX_FISH_PER_BUBBLE             = 5
FISH_VISUAL_SIZE                = 124 x 124 (fixed)
```

Important:

- A waiting tray count of **5 means immediate level failure** in our current build.
- Do not rescue the player after the fifth waiting fish is committed.
- A bubble never contains more than **5 fish** (`GameConfig.MaxFishPerBubble`). Later levels get harder by adding bubbles, total fish, FishTypes and varied bubble compositions, not by exceeding 5 fish per bubble.
- Fish keep one fixed visual size (124 x 124) in bubbles, in flight and in tanks. Fish are never resized by bubble occupancy, reflow or level.
- The reference contains an “Out of Space” rescue offer, but that system is **disabled for the current product rule** unless explicitly enabled later.

---

# 2. PRODUCT SUMMARY

Genre:

```text
Casual mobile sorting puzzle
Target-based fish routing
Portrait orientation
No countdown fail timer
```

Core challenge:

```text
Several tanks request different fish types at the same time.

Player taps fish inside bubbles.

If a fish matches any active tank target:
    it goes directly into that tank.

If it matches no active tank:
    it goes to the five-slot Waiting Tray.

Complete each tank with 3 matching fish.

When a tank completes:
    progress increases,
    that tank receives another target group.

When targets change:
    compatible fish already waiting in the tray
    automatically move into matching tanks.

Empty bubbles pop.

The bubble pile above settles downward,
like eggs sliding/falling into a gap.

Reach the total fish objective before
the Waiting Tray reaches five fish.
```

---

# 3. IMPORTANT REFERENCE OBSERVATIONS

The supplied screenshots establish the following visual/gameplay structure:

## Top area

- Level indicator on upper-left.
- Currency on upper-right.
- Life/heart status may also be shown in some states.
- A shelf holds **4 aquarium/tank positions**.
- More than one tank can be active at once.
- Locked tanks are represented with a large green `+`.
- Each active tank has a hanging target badge underneath.
- Target badge shows:
  - fish icon,
  - current tank progress such as `0/3`, `1/3`, `2/3`.

## Middle area

- A global objective/progress block appears on the left, e.g.:

```text
Cá
0 / 102
```

- A five-slot Waiting Tray appears below the tank area.
- Large transparent bubbles fill the main play area.
- Bubbles form an irregular packed pile.
- Bubbles may overlap visually.
- Fish are visibly contained inside each bubble.

## Bottom area

- Progression/booster/settings controls may appear.
- Some controls are level-locked.
- These are not required for the first core build.

## Reference overlays observed

- Level fail panel with heart `-1`.
- “Out of Space” rescue offer.
- Level complete reward screen.
- Extra tank unlock modal:
  - 600 Gold,
  - or rewarded ad/free route.

Only rules explicitly activated in this document should be implemented in the MVP.

---

# 4. CORE LOOP

```text
Observe:
- active tank targets
- active tank fill counts
- Waiting Tray
- bubble pile
- global progress

        ↓

Tap one valid fish inside a bubble

        ↓

Reserve/remove fish from source bubble

        ↓

Does fish match any active tank target?

      /                         \
    YES                          NO
     ↓                            ↓
Route to matching tank        Route to Waiting Tray
     ↓                            ↓
Tank progress +1             Tray count +1
     ↓                            ↓
Tank reached 3?              Tray count == 5?
   /       \                    /        \
 NO        YES                NO         YES
 ↓          ↓                  ↓           ↓
continue   Resolve tank      continue      LOSE
             ↓
         Global progress +3
             ↓
        Assign next target
             ↓
     Auto-promote tray fish
             ↓
       Cascade if possible

        ↓

Was source bubble emptied?

        ↓

Bubble pop
        ↓
Bubble pile settles downward
        ↓
New bubble may enter from above

        ↓

Global objective complete?

     /           \
   YES            NO
    ↓              ↓
   WIN         Unlock input
```

---

# 5. FISH IDENTITY

Gameplay equality must use stable logical IDs.

Example:

```csharp
public enum FishType
{
    Orange,
    GreenStriped,
    RedClown,
    PinkStriped,
    BlackStriped,
    Yellow,
    GreySpotted,
    Pink,
    Koi,
    Crab,
    Snail
}
```

Actual names may change with asset production.

Hard rule:

```text
FishType is gameplay identity.
Sprite/display color/name is presentation only.
```

Never match by:

- Sprite name.
- GameObject name.
- Pixel color.
- Display label.
- Prefab filename.

---

# 6. MULTI-TANK SYSTEM

This replaces the previous single-target-tank assumption.

The level has:

```text
4 physical TankSlots
```

Default:

```text
TankSlot 0 = Unlocked
TankSlot 1 = Unlocked
TankSlot 2 = Locked
TankSlot 3 = Locked
```

Configuration may change this per level later, but the reference-compatible default is:

```text
2 active tanks + 2 optional tanks
```

---

# 7. TANK STATE

Each unlocked tank independently owns:

```text
TankSlotId
IsUnlocked
CurrentTargetFishType
FillCount
ContainedFish
TankState
```

Possible logical tank states:

```csharp
public enum TankState
{
    Locked,
    WaitingForTarget,
    AcceptingFish,
    ResolvingTriple,
    CompletedNoMoreTargets
}
```

Capacity:

```text
TANK_CAPACITY = 3
```

Invariant:

```text
0 <= FillCount <= 3
```

When accepting fish:

```text
Every fish in that tank must match CurrentTargetFishType.
```

---

# 8. TARGET GROUP QUEUE

Do not use one global `CurrentTargetFishType`.

There are multiple simultaneous target types, one per active tank.

Level data contains:

```csharp
List<FishType> targetGroupQueue;
```

One queue entry represents:

```text
one complete group of 3 fish
```

Example:

```text
targetGroupQueue:

Orange
GreenStriped
RedClown
PinkStriped
Orange
Yellow
GreenStriped
...
```

At level start:

```text
Tank 0 receives queue[0]
Tank 1 receives queue[1]

Tank 2/3 receive nothing while locked.
```

When one active tank completes:

```text
that tank consumes the next available targetGroupQueue entry.
```

If queue has no remaining target:

```text
TankState = CompletedNoMoreTargets
```

and it stops accepting fish.

---

# 9. UNLOCKING AN EXTRA TANK

A locked TankSlot displays a green `+`.

Player may tap it and open the unlock modal.

Available routes:

```text
A. Pay 600 Gold
B. Complete rewarded ad
```

On successful unlock:

1. Mark TankSlot unlocked for the configured scope.
2. Assign the next unassigned target group from `targetGroupQueue`.
3. Update hanging target badge.
4. Run Waiting Tray auto-promotion against all active targets.
5. Only then return to player input.

## 9.1 Unlock scope

For the first implementation:

```text
Tank unlock is level-attempt scoped by default.
```

Meaning:

- extra tank exists for the current active level attempt,
- a fresh level begins from the level's configured unlocked tank count.

Do not make it permanent account progression unless explicitly changed later.

Keep scope configurable:

```csharp
public enum TankUnlockScope
{
    LevelAttempt,
    Level,
    Permanent
}
```

Default:

```text
LevelAttempt
```

---

# 10. GOLD UNLOCK RULE

Cost:

```text
600 Gold
```

On gold unlock:

```text
if Gold < 600:
    reject
    do not mutate Gold
    show insufficient feedback

if Gold >= 600:
    deduct exactly 600
    unlock selected tank
```

Gold mutations must go through one wallet service.

Do not let UI scripts mutate wallet values directly.

---

# 11. REWARDED AD UNLOCK RULE

Use abstraction:

```csharp
public interface IRewardedAdService
{
    void Show(
        Action onRewardEarned,
        Action onClosedWithoutReward,
        Action onFailed
    );
}
```

Rules:

- Opening an ad is not enough.
- Closing an ad is not enough.
- Tank unlocks only after reward callback.
- Rewarded unlock costs 0 Gold.

Editor:

```text
Use MockRewardedAdService.
```

Real ad SDK comes later.

---

# 12. TARGET BADGE UNDER EACH TANK

Each active tank has a hanging target card/badge beneath it.

It shows:

```text
CurrentTargetFish icon
FillCount / 3
```

Examples:

```text
Orange fish     0/3
Green fish      2/3
Pink fish       1/3
```

Rules:

- Update counter only after fish is logically committed to tank.
- On triple completion:
  - briefly show `3/3`,
  - play completion feedback,
  - then transition to the next target icon and `0/3`.

Locked tanks do not show target fish.

---

# 13. GLOBAL OBJECTIVE / LEFT PROGRESS BLOCK

The screenshot shows a global fish progress counter such as:

```text
Cá
0 / 102
```

Therefore the primary player-facing objective is:

```text
CollectedFishCount / TotalFishRequired
```

Example:

```text
0 / 102
3 / 102
6 / 102
...
102 / 102
```

Each resolved tank triple contributes:

```text
+3 CollectedFishCount
```

The level wins when:

```text
CollectedFishCount == TotalFishRequired
```

Internal per-type counts should still exist for validation/debugging even if they are not shown to the user.

---

# 14. LEVEL COMPLETION DATA

For each FishType:

```text
Number of that FishType in all bubbles
==
Number of that FishType in targetGroupQueue * 3
```

Global:

```text
TotalFishRequired
==
targetGroupQueue.Count * 3
==
Total number of fish stored across all bubble definitions
```

This is mandatory.

There must be:

- no extra fish,
- no missing fish,
- no unused target groups.

---

# 15. ROUTING A PLAYER-TAPPED FISH

When the player taps one valid fish:

```text
FishType selectedType
```

Find every active accepting tank satisfying:

```text
tank.CurrentTargetFishType == selectedType
```

## 15.1 No matching active tank

If none exists:

```text
route fish to Waiting Tray
```

## 15.2 Exactly one matching active tank

Route directly into that tank.

## 15.3 Multiple matching active tanks

Use deterministic routing:

```text
1. Prefer tank with highest FillCount.
2. If tied, prefer lowest TankSlot index.
```

Reason:

- completes partially filled tank first,
- creates new target sooner,
- reduces ambiguity,
- is deterministic.

Do not use random routing.

---

# 16. WAITING TRAY

There are exactly:

```text
5 visual/logical slots
```

Ordered left-to-right:

```text
Slot 0
Slot 1
Slot 2
Slot 3
Slot 4
```

Waiting fish maintain stable order.

Invariant during healthy gameplay:

```text
0 <= WaitingTray.Count <= 4
```

State:

```text
WaitingTray.Count == 5
```

is terminal failure under current rules.

---

# 17. HARD LOSS RULE

If player taps a fish that matches no active target:

1. Fish is routed to Waiting Tray.
2. Fish is inserted logically.
3. If count becomes exactly 5:

```text
LOSE immediately
```

Important:

```text
DO NOT:
- wait for target change,
- run auto-promotion to save the player,
- unlock another tank automatically,
- clear tray automatically,
- inspect future target groups,
- postpone failure until next tap.
```

Gameplay failure is committed at the moment the fifth waiting fish is inserted.

Presentation may allow the landing animation to finish before showing the lose panel.

---

# 18. REFERENCE “OUT OF SPACE” OFFER

The supplied reference contains a rescue screen where a full waiting tray can be cleared by:

```text
Gold
or
rewarded ad
```

It also shows a Fail Offer.

This is an observed monetization mechanic.

However, current project rule explicitly states:

```text
Waiting Tray reaches 5 → LOSE
```

Therefore:

```csharp
OutOfSpaceRescueEnabled = false;
```

for the MVP.

Design interfaces so this feature can be added later without rewriting the tray system.

Do not implement the 900-Gold rescue unless explicitly requested.

---

# 19. AUTO-PROMOTION FROM WAITING TRAY

This mechanic now checks against **all active tank targets**, not one target.

Trigger auto-promotion after:

- a tank completes and receives a new target,
- an extra tank is successfully unlocked and receives a target.

Optional later trigger:

- level loading after state restoration.

---

# 20. AUTO-PROMOTION ALGORITHM

Use deterministic tray order.

Pseudo:

```text
repeat:
    movedAnyFish = false

    scan Waiting Tray from left → right

    for each waiting fish:
        find matching active tanks

        if no matching tank:
            continue

        choose target tank using:
            highest FillCount
            then lowest TankSlot index

        remove fish from Waiting Tray
        compact tray
        animate fish → tank
        add fish to tank

        movedAnyFish = true

        if target tank FillCount == 3:
            resolve tank
            assign next target
            restart tray scan from Slot 0

        break current scan and restart

until no waiting fish can move
or level is Win/Lose
```

Input remains locked for the entire cascade.

---

# 21. TANK TRIPLE RESOLUTION

When a tank reaches:

```text
3 / 3
```

Sequence:

```text
1. Lock tank input/routing.
2. Display 3/3.
3. Play tank completion animation.
4. Commit global progress +3.
5. Consume/remove the 3 fish.
6. Tank becomes empty.
7. Draw next target group if available.
8. Update hanging target badge.
9. Run Waiting Tray auto-promotion.
```

Progress mutation must occur exactly once.

Use idempotent state transition.

---

# 22. WIN CONDITION

Primary:

```text
CollectedFishCount == TotalFishRequired
```

With valid level data, this must also imply:

```text
targetGroupQueue fully consumed
no target groups remain active
no fish remain in bubbles
WaitingTray.Count == 0
no unresolved tank fish remain
```

In development builds assert those invariants.

If progress reaches target while fish remain:

```text
treat as LevelData corruption.
```

Do not silently accept inconsistent data.

---

# 23. WIN REWARD

Explicit current product rule (M12.1 — overrides the earlier "+20 Score" rule):

```text
Win one level → +20 Gold
```

Award exactly once per completed attempt. Re-opening the Win panel or a duplicate outcome callback must not award again.

Authoritative method:

```text
WalletService.AwardLevelCompletion(GameConfig.LevelCompleteGoldReward = 20)
```

The Win screen shows the reward as coin icon + `+20` (Gold), and the top Gold HUD updates as soon as the reward commits.

Gold and Hearts persist across Next Level, Replay, Retry and level transitions; a new LevelSession never resets them.
WalletService owns Gold (win reward, tank unlock spend of 600); LifeService owns Hearts. UI never mutates either.

There is no standalone Score number in the gameplay HUD. The HUD shows only: Level badge | Gold | Hearts.

---

# 24. LOSE / LIFE RULE

Explicit rule:

```text
Lose one level → -1 Heart
```

On authoritative Lose transition:

```text
Lives -= 1 exactly once
```

Do not deduct heart again when:

- lose popup opens,
- retry is tapped,
- fail animation repeats,
- scene reloads.

Use idempotency guard/state transition.

---

# 25. LIFE TIMER OBSERVATION

Reference capture shows:

```text
Heart count + countdown timer
```

This suggests heart regeneration.

Heart regeneration is:

```text
REFERENCE OBSERVED
NOT REQUIRED FOR CORE MVP
```

Architecture may reserve:

```text
NextLifeAt
LifeRegenInterval
```

but do not build regeneration before core gameplay is stable.

---

# 26. BUBBLE MODEL — MAJOR REVISION

A bubble is **not** simply destroyed and replaced in the same fixed location.

The play area behaves like a **pile of eggs / packed bubbles under gravity**.

Mental model:

```text
Bubbles are stacked in a pile.

One bubble becomes empty and pops.

That leaves a hole.

Bubbles positioned above/near that hole
fall or slide downward into lower available spaces.

The movement can cascade upward.

If more BubbleData remain,
new bubble(s) enter from above the screen/pile
to replenish the top.
```

This behavior is a core presentation/game-feel requirement.

---

# 27. BUBBLE CONTENT RULE

Every standard bubble contains fish from exactly:

```text
3 distinct FishTypes
```

This does **not** mean exactly 3 total fish.

A bubble holds at most **5 fish** (`MAX_FISH_PER_BUBBLE`).
Preferred patterns: 3 fish = 1+1+1, 4 fish = 2+1+1, 5 fish = 2+2+1 or 3+1+1.

Examples:

Valid:

```text
Orange
Orange
Green
Pink
Green

Distinct = Orange, Green, Pink = 3
Total = 5 (maximum)
```

Invalid:

```text
Orange
Orange
Green

Distinct = 2
```

unless a future tutorial/obstacle type explicitly overrides the rule.

---

# 28. BUBBLE FISH INTERACTION

Player taps directly on a visible/interactable fish inside a bubble.

A fish is tappable if:

```text
GameState == PlayerInput
Source bubble is Active
FishState == Idle
Fish is visible/interactable
No modal blocks input
```

On valid selection:

```text
FishState:
Idle
→ Reserved
→ InTransit
→ Tank / WaitingTray / Consumed
```

Reservation must occur immediately to prevent double taps.

---

# 29. INTERNAL FISH LAYOUT INSIDE A BUBBLE

A bubble may contain multiple fish.

When one fish leaves:

```text
remaining fish should visually reflow
into a tidy bubble layout
```

Do not let random holes accumulate.

Use predefined visual formations per remaining count.

Example:

```text
1 fish  → center
2 fish  → left/right
3 fish  → triangle
4 fish  → 2x2-ish
5 fish  → packed pentagon
```

There is no 6-fish formation (maximum 5 fish per bubble).

Fish size is fixed at 124 x 124. The layout controls POSITION only:
- never shrink fish because a bubble holds more fish,
- never resize remaining fish when one leaves (reflow moves positions only),
- spread positions instead; small overlap and slight overflow past the rim are allowed,
- no Mask / RectMask2D clipping. Order stays BubbleBack → Fish → BubbleFront.

Inside a tank, fish keep the same 124 size and overlap with small offsets
(1: center, 2: (-28,4)/(28,-4), 3: (-40,7)/(0,-6)/(40,7)); later fish draw in front.

These layouts affect presentation only.

Logical ordering may remain stable.

Animate remaining fish to their new local positions.

---

# 30. BUBBLE EMPTY RULE

A bubble becomes empty only when:

```text
bubble.RemainingFishCount == 0
```

Then:

1. Mark bubble non-interactable.
2. Mark its pile slot as becoming vacant.
3. Play bubble-empty reaction.
4. Play pop animation/VFX.
5. Remove/pool bubble visual.
6. Remove bubble occupant from pile slot.
7. Run `BubblePileResolver`.
8. Animate surviving bubbles falling/sliding into new slots.
9. Feed new bubble from top queue if available.
10. Return to PlayerInput only after pile settles.

---

# 31. DO NOT USE FREE RIGIDBODY PHYSICS FOR THE PUZZLE

Do not make gameplay depend on uncontrolled Unity Rigidbody2D collisions.

Reason:

- nondeterministic,
- difficult to reproduce,
- bubbles can jitter,
- bubbles may block taps,
- mobile performance is less predictable,
- level layout becomes hard to author.

Use:

```text
deterministic logical pile slots
+
authored gravity relationships
+
tweened visual movement
```

The motion should look physical.

The logic should remain deterministic.

---

# 32. BUBBLE PILE LAYOUT

Represent the visible pile as authored `BubblePileSlot`s.

Each slot contains:

```text
slotId
anchoredPosition
visualScale
sortingOrder/depth
row
column
downCandidateSlotIds
spawnEligible
```

Conceptual staggered layout:

```text
        [09]     [10]

    [06]    [07]    [08]

 [02]    [03]    [04]    [05]

      [00]       [01]
```

Actual slot positions must match the supplied/reference art layout, not this ASCII example.

---

# 33. BUBBLE GRAVITY GRAPH

Each slot has deterministic positions it may fall toward.

Example:

```text
Slot 10 can fall to:
7
8

Slot 8 can fall to:
4
5
```

When a lower candidate becomes empty, a bubble can slide/fall down.

Selection priority for possible destination:

```text
1. Lowest Y destination.
2. Shortest horizontal movement.
3. Lowest destination slotId as tie-break.
```

This creates predictable egg-pile behavior.

---

# 34. BUBBLE PILE SETTLE ALGORITHM

After one bubble pops:

```text
repeat until stable:

    find movable bubbles,
    ordered from lower row to higher row,
    then left-to-right

    for each bubble:
        inspect its configured downCandidate slots

        if one or more destination slots are empty:
            select best destination
            move logical occupancy
            record animation move
            continue settling

stop when no bubble can move lower
```

Alternative implementation is acceptable if it produces the same deterministic results.

Never use random selection.

---

# 35. CASCADING “EGG PILE” BEHAVIOR

Example:

Before:

```text
      A
   B     C
 D   E   F
```

Bubble `E` empties:

```text
      A
   B     C
 D   _   F
```

A nearby supported bubble such as `B`, `C`, or `A`
moves according to the slot gravity graph:

```text
      _
   B     A
 D   C   F
```

Exact route depends on authored slot graph.

Visual requirement:

```text
bubble above
↓
small drop/slide
↓
soft bounce at destination
↓
next bubble in chain may move
```

It should feel like eggs settling into the missing space.

---

# 36. NEW BUBBLES ENTER FROM ABOVE

The level may contain more bubble definitions than visible pile capacity.

Data:

```text
bubbleQueue
```

After the visible pile finishes settling:

```text
while there is an eligible empty TOP spawn slot
and bubbleQueue not empty:
    dequeue next BubbleData
    spawn bubble above visible pile
    animate downward into top slot
```

Important:

```text
New bubble does NOT magically appear
inside the exact popped lower slot.
```

Lower holes are filled by existing bubbles sliding/falling.

New bubbles replenish from the top.

---

# 37. BUBBLE SPAWN ANIMATION

Desired motion:

```text
spawn above target top slot
↓
fall/float downward
↓
small squash/bounce
↓
settle
```

Bubble becomes interactable only after:

```text
global pile resolution is finished
AND
GameState returns to PlayerInput
```

---

# 38. BUBBLE PILE INPUT LOCK

During:

- bubble pop,
- bubble falling,
- bubble sliding,
- top replenishment,

fish input must be locked.

Reason:

```text
fish world/screen positions are moving
```

Prevent accidental tap against an outdated collider/slot.

---

# 39. FROZEN/NUMBERED BUBBLE OBSERVATION

Reference screenshot shows at least one visually frozen bubble with a large number.

This indicates a future obstacle/locked-bubble mechanic.

Current MVP:

```text
DO NOT IMPLEMENT.
```

Reserve architecture for bubble modifiers:

```csharp
public enum BubbleModifier
{
    None,
    Frozen,
    Locked,
    Other
}
```

But standard bubbles use:

```text
None
```

---

# 40. LEVEL DATA MODEL

Recommended:

```csharp
[CreateAssetMenu(menuName = "FishGame/Level")]
public class LevelData : ScriptableObject
{
    public string levelId;

    public int defaultUnlockedTankCount = 2;
    public int maxTankSlots = 4;

    public int totalFishRequired;

    // One entry = one group of 3.
    public List<FishType> targetGroupQueue;

    // More entries than visible pile capacity are allowed.
    public List<BubbleDefinition> bubbleQueue;

    public BubblePileLayout pileLayout;
}
```

---

# 41. BUBBLE DEFINITION

```csharp
[Serializable]
public class BubbleDefinition
{
    public string bubbleId;
    public List<FishType> fishes;
    public BubbleModifier modifier = BubbleModifier.None;
}
```

Standard validator requires:

```text
Distinct FishTypes == 3
1 <= fishes.Count <= MAX_FISH_PER_BUBBLE (5)
```

---

# 42. BUBBLE PILE LAYOUT DATA

Suggested:

```csharp
[CreateAssetMenu(menuName = "FishGame/Bubble Pile Layout")]
public class BubblePileLayout : ScriptableObject
{
    public List<BubblePileSlotDefinition> slots;
}
```

Slot:

```csharp
[Serializable]
public class BubblePileSlotDefinition
{
    public int slotId;

    public Vector2 anchoredPosition;

    public int row;
    public int column;

    public int sortingOrder;

    public List<int> downCandidateSlotIds;

    public bool canReceiveSpawnFromTop;
}
```

---

# 43. LEVEL VALIDATION — MANDATORY

Before level starts:

## Tanks

```text
1 <= defaultUnlockedTankCount <= maxTankSlots
maxTankSlots == 4 for current reference layout
```

## Target groups

```text
targetGroupQueue.Count > 0
totalFishRequired == targetGroupQueue.Count * 3
```

## Fish population

For every FishType:

```text
FishInAllBubbles(type)
==
TargetGroupOccurrences(type) * 3
```

Global:

```text
TotalBubbleFish == totalFishRequired
```

## Bubbles

For every standard bubble:

```text
fishes.Count > 0
fishes.Count <= MAX_FISH_PER_BUBBLE (5)
DistinctFishTypes == 3
```

## Pile layout

```text
slot IDs unique
downCandidate IDs valid
no direct self-reference
no impossible cyclic downward graph
at least one top spawn slot exists
```

If invalid:

```text
DO NOT start gameplay.
```

Show/log actionable errors.

---

# 44. EXACT PLAYER TAP RESOLUTION ORDER

This order is authoritative.

```text
1. Verify GameState == PlayerInput.

2. Verify FishState == Idle.

3. Verify source bubble active/interactable.

4. Set GameState = RoutingFish.

5. Mark fish Reserved immediately.

6. Remove/reserve fish logically from source BubbleData runtime state.

7. Determine matching active tanks.

8A. If matching tank exists:
       select deterministic target tank.
       animate fish → tank.
       commit fish to tank.
       increment that tank FillCount.

       if FillCount == 3:
           resolve completed tank.
           +3 global progress.
           assign next target.
           run Waiting Tray auto-promotion cascade.

8B. If no matching tank:
       animate fish → next Waiting Tray slot.
       commit fish into Waiting Tray.

       if WaitingTray.Count >= 5:
           commit LOSE immediately.
           stop normal resolution.

9. Check whether source bubble became empty.

10. If source bubble empty and level not already Lose:
       pop bubble.
       settle bubble pile.
       replenish new top bubble(s).

11. Check Win.

12. If neither Win nor Lose:
       GameState = PlayerInput.
```

If Lose was committed at step 8B:

- gameplay is already terminal,
- optional visual cleanup can finish,
- no gameplay rescue/target reassignment should run.

---

# 45. GAME STATE MACHINE

Suggested:

```csharp
public enum GameState
{
    Boot,
    LoadingLevel,
    InitializingTanks,
    InitializingPile,

    PlayerInput,

    RoutingFish,
    ResolvingTank,
    AssigningTarget,
    AutoPromotingTray,

    PoppingBubble,
    SettlingBubblePile,
    SpawningTopBubble,

    TankUnlockModal,

    Win,
    Lose,
    Paused
}
```

There must be one authoritative owner of state transitions.

---

# 46. PLAYER INPUT RULE

Fish input accepted only when:

```text
GameState == PlayerInput
```

No fish input during:

- fish travel,
- tank resolve,
- target change,
- tray auto-promotion,
- bubble pop,
- bubble settle,
- bubble spawn,
- tank unlock modal,
- win,
- lose,
- pause.

---

# 47. ANIMATION CONTRACT — OVERALL

All motion values must be tunable through config/Inspector.

Do not scatter animation magic numbers through gameplay code.

Recommended:

```csharp
[Serializable]
public class AnimationTuning
{
    public float fishTapSquashDuration;
    public float fishRouteDuration;
    public float fishLandingBounceDuration;

    public float tankResolveDuration;
    public float tankTargetSwapDuration;

    public float trayAutoMoveDuration;
    public float trayAutoMoveStagger;

    public float bubbleFishReflowDuration;

    public float bubblePopDuration;
    public float bubbleFallDuration;
    public float bubbleSlideDuration;
    public float bubbleLandingBounceDuration;
    public float bubbleTopSpawnDuration;
}
```

---

# 48. FISH IDLE ANIMATION

Fish inside a bubble should feel alive.

Use subtle:

- bobbing,
- tail/body wiggle,
- slight local drift,
- desynchronized phase.

Idle animation:

```text
must not mutate gameplay coordinates/state
```

Hit areas should remain predictable.

---

# 49. BUBBLE IDLE ANIMATION

Bubbles use subtle:

- vertical breathing,
- tiny scale pulse,
- highlight shimmer,
- slight wobble.

Do not use idle movement large enough to break pile readability.

---

# 50. FISH TAP FEEDBACK

On valid tap:

```text
small squash
→ slight stretch
→ route launch
```

Reserve fish logically before this animation completes.

---

# 51. FISH → TANK ANIMATION

Desired:

```text
bubble fish position
↓
smooth curved/Bezier trajectory
↓
tank interior slot
↓
small landing bounce
```

Fish should visibly end inside the correct aquarium.

Tank visual may react with:

- small bounce,
- water ripple,
- sparkle/bubble.

---

# 52. FISH → WAITING TRAY ANIMATION

Desired:

```text
bubble
↓
curved path
↓
next left-to-right tray slot
↓
small settle/squash
```

When the fifth fish lands:

```text
logical Lose is already committed
```

Then:

```text
tray danger response
↓
screen fail feedback
↓
lose panel
```

---

# 53. WAITING TRAY COMPACTION

When one or more waiting fish auto-promote:

```text
remaining waiting fish compact left
```

Animate slot movement.

Example:

```text
Before:
[A][Blue][C][Blue][_]

Blue fish leave.

After:
[A][C][_][_][_]
```

Do not leave visual gaps.

---

# 54. AUTO-PROMOTION ANIMATION

When new tank targets become available:

```text
matching waiting fish
→ jump/fly upward into appropriate tanks
```

Rules:

- left-to-right tray scan,
- one logical move at a time,
- small visual stagger,
- tanks may complete,
- new targets may appear,
- cascade may continue.

Input locked for entire chain.

---

# 55. TANK COMPLETE ANIMATION

When a tank reaches 3:

```text
third fish lands
↓
badge shows 3/3
↓
three fish react/swim/bounce
↓
water sparkle/pop
↓
global objective +3
↓
fish clear from tank
↓
old target badge exits
↓
new target badge enters
↓
0/3
```

Exact visual staging can be tuned after asset creation.

---

# 56. BUBBLE POP ANIMATION

When final fish leaves bubble:

```text
bubble becomes non-interactable
↓
wobble/compress
↓
small expand
↓
POP
↓
bubble fragments / water-bubble particles
↓
vacant pile slot created
```

Then immediately:

```text
BubblePileResolver
```

takes control.

---

# 57. BUBBLE FALL / SLIDE ANIMATION

This is a critical reference behavior.

When a bubble moves into a lower gap:

```text
start at existing pile slot
↓
follow short falling/sliding curve
↓
accelerate slightly downward
↓
settle into destination
↓
soft squash/bounce
```

If a chain of bubbles moves:

```text
lower move starts first
upper bubbles follow quickly
```

Result should resemble:

```text
a pile of eggs collapsing into a missing egg position
```

not:

```text
teleport / fade / respawn
```

---

# 58. TOP REPLENISHMENT ANIMATION

After pile settles:

```text
new bubble appears above top boundary
↓
falls/floats into top spawn slot
↓
soft landing
↓
pile stable
```

Do not spawn directly into the popped lower location.

---

# 59. LAYOUT REFERENCE

Logical screen hierarchy:

```text
┌──────────────────────────────┐
│ Level            Gold/Lives │
│                              │
│  Tank  Tank  +Tank  +Tank   │
│ Target Target               │
│                              │
│ Global Fish Progress        │
│                              │
│ [ ][ ][ ][ ][ ] Waiting Tray│
│                              │
│      Bubble Pile             │
│  ○   ○    ○   ○             │
│    ○   ○     ○              │
│ ○    ○   ○      ○           │
│                              │
│ bottom progression/settings  │
└──────────────────────────────┘
```

Exact pixel anchors must be finalized during Asset/UI specification.

---

# 60. MOBILE CANVAS

Reference resolution:

```text
1080 x 1920
Portrait
```

Unity:

```text
Canvas Scaler
Scale With Screen Size
Reference Resolution = 1080 x 1920
Match = 0.5
```

Use safe-area handling.

---

# 61. TECHNICAL ARCHITECTURE

Recommended:

```text
Assets/Game/
├── Scripts/
│   ├── Core/
│   │   ├── GameState.cs
│   │   ├── GameFlowController.cs
│   │   ├── GameConfig.cs
│   │   └── GameEvents.cs
│   │
│   ├── Domain/
│   │   ├── FishType.cs
│   │   ├── FishRuntimeState.cs
│   │   ├── TankRuntimeState.cs
│   │   ├── WaitingTrayState.cs
│   │   ├── LevelRuntimeState.cs
│   │   └── LevelValidator.cs
│   │
│   ├── Tanks/
│   │   ├── TankBoardController.cs
│   │   ├── TankSlotController.cs
│   │   ├── TargetGroupQueue.cs
│   │   ├── TankUnlockController.cs
│   │   └── FishRoutingService.cs
│   │
│   ├── Tray/
│   │   ├── WaitingTrayController.cs
│   │   └── TrayAutoPromotionService.cs
│   │
│   ├── Bubbles/
│   │   ├── BubbleController.cs
│   │   ├── BubbleFishLayoutController.cs
│   │   ├── BubblePileController.cs
│   │   ├── BubblePileResolver.cs
│   │   └── BubbleSpawner.cs
│   │
│   ├── Presentation/
│   │   ├── FishView.cs
│   │   ├── TankView.cs
│   │   ├── TankTargetBadgeView.cs
│   │   ├── WaitingTrayView.cs
│   │   ├── BubbleView.cs
│   │   ├── GlobalProgressView.cs
│   │   ├── HUDView.cs
│   │   └── ResultView.cs
│   │
│   ├── Progression/
│   │   ├── ScoreService.cs
│   │   ├── WalletService.cs
│   │   ├── LifeService.cs
│   │   └── PlayerProgress.cs
│   │
│   ├── Ads/
│   │   ├── IRewardedAdService.cs
│   │   └── MockRewardedAdService.cs
│   │
│   └── Save/
│       ├── ISaveService.cs
│       └── LocalJsonSaveService.cs
│
├── Data/
│   ├── Levels/
│   ├── BubblePileLayouts/
│   └── Config/
│
├── Prefabs/
│   ├── Fish/
│   ├── Bubbles/
│   ├── Tanks/
│   └── UI/
│
├── Art/
├── Audio/
├── VFX/
└── Scenes/
```

---

# 62. RESPONSIBILITY BOUNDARIES

## GameFlowController

Owns:

- authoritative GameState,
- top-level turn resolution,
- input locking,
- Win/Lose transition.

Must not become a giant all-purpose manager.

---

## FishRoutingService

Owns:

```text
Fish tapped → choose:
matching tank
or
Waiting Tray
```

Also owns deterministic matching-tank selection policy.

Does not animate.

---

## TankBoardController

Owns:

- 4 TankSlots,
- locked/unlocked availability,
- current target assignment,
- next target draw.

---

## TankSlotController / state

Owns:

- current target,
- fill count,
- contained fish,
- completion event.

Does not mutate global score/gold/lives.

---

## WaitingTrayController

Owns:

- ordered 5 slots,
- insertion,
- removal,
- compaction,
- fail threshold notification.

It does not decide heart deduction.

---

## TrayAutoPromotionService

Owns:

- deterministic scan,
- matching against all active tanks,
- cascade resolution.

---

## BubblePileController

Owns:

- slot occupancy,
- pile runtime state,
- bubble queue,
- stable settled state.

---

## BubblePileResolver

Owns:

- deterministic gravity/slide resolution.

Does not use Rigidbody2D as source of truth.

---

## Views

May:

- animate,
- play particles,
- update sprites/text,
- play UI feedback.

May not decide:

- target assignment,
- Win/Lose,
- wallet mutation,
- lives mutation,
- score mutation,
- level completion.

---

# 63. LOCAL PROGRESS DATA

Suggested:

```csharp
public class PlayerProgress
{
    public int currentLevel;
    public int score;
    public int gold;
    public int currentLives;
}
```

Tank attempt unlock state belongs to current level runtime unless scope changes.

Do not store temporary attempt state as permanent account progression unless required.

---

# 64. GAMEPLAY DATA VS PRESENTATION

Domain:

```text
FishType
Tank target
Tank fill count
Waiting tray contents
Bubble fish contents
Bubble pile slot occupancy
Target queue
Global progress
Lives/Gold/Score
```

Presentation:

```text
Sprites
Fish positions
Bubble wobble
Tank water animation
Tween paths
Particles
Audio
Haptic
Popup transitions
```

Presentation failure must never silently corrupt domain state.

---

# 65. TEST MATRIX — MANDATORY

## T01 — Fish matches one tank

Given:

```text
Tank0 target = Orange 1/3
Tank1 target = Green 0/3
```

Tap Orange.

Expected:

```text
Orange → Tank0
Tank0 = 2/3
Tray unchanged
```

---

## T02 — Fish matches no tank

Targets:

```text
Orange
Green
```

Tap Pink.

Expected:

```text
Pink → Waiting Tray
```

---

## T03 — Fish matches multiple tanks

Tank0:

```text
Orange 1/3
```

Tank1:

```text
Orange 2/3
```

Tap Orange.

Expected:

```text
route to Tank1
Tank1 completes first
```

---

## T04 — Tank reaches 3

Expected:

```text
show 3/3
resolve triple
global progress +3
tank clears
next target assigned
tray auto-promotion runs
```

---

## T05 — Fifth Waiting Tray fish = immediate loss

Given:

```text
Tray.Count = 4
```

Tap non-target fish.

Expected:

```text
Tray.Count = 5
GameState = Lose
Lives -1 exactly once
No auto-rescue
No target reassignment rescue
```

This test is critical.

---

## T06 — Auto-promote after target change

Tray:

```text
Red, Blue, Green
```

A tank receives:

```text
Blue target
```

Expected:

```text
Blue leaves tray automatically
enters matching tank
tray compacts
```

---

## T07 — Auto-promotion cascade

Tray:

```text
Blue, Red, Blue, Red
```

Tank gets Blue while already containing one Blue.

Expected:

```text
2 Blue promote
Blue tank completes
new target becomes Red
Red fish promote
```

Input locked throughout.

---

## T08 — Unlock third tank for 600 Gold

Given:

```text
Gold = 1000
Tank2 locked
```

Unlock Tank2.

Expected:

```text
Gold = 400
Tank2 unlocked
next target assigned
auto-promotion runs
```

---

## T09 — Insufficient Gold

Gold:

```text
599
```

Expected:

```text
Tank remains locked
Gold unchanged
insufficient feedback
```

---

## T10 — Rewarded ad unlock

Mock reward callback succeeds.

Expected:

```text
Tank unlocked
Gold unchanged
target assigned
```

---

## T11 — Rewarded ad canceled

Expected:

```text
Tank remains locked
Gold unchanged
```

---

## T12 — Bubble still contains fish

Remove one fish from non-empty bubble.

Expected:

```text
bubble remains
remaining fish reflow
pile does not settle
```

---

## T13 — Empty bubble pops

Remove last fish.

Expected:

```text
bubble input disabled
bubble pops
pile slot becomes empty
BubblePileResolver runs
```

---

## T14 — Bubble above falls into gap

Given authored pile graph:

```text
Lower slot becomes empty.
Upper bubble has that lower slot as valid destination.
```

Expected:

```text
upper bubble moves logically to lower slot
visual fall/slide
soft landing
```

---

## T15 — Multi-step pile cascade

Create a gap that causes:

```text
Bubble A falls
then Bubble B can fall into A's previous slot
```

Expected:

```text
both moves resolve deterministically
pile ends stable
```

---

## T16 — New bubble enters from top

After pile settles:

```text
top spawn slot empty
bubbleQueue not empty
```

Expected:

```text
next bubble spawns above
falls into top slot
```

It must not spawn in the original popped lower slot.

---

## T17 — Bubble three-type validation

Bubble:

```text
Orange, Orange, Green
```

Distinct types = 2.

Expected:

```text
Level validation fails
```

---

## T18 — Exact per-type population

Target groups:

```text
Orange x2 groups
Green x1 group
```

Required:

```text
Orange = 6
Green = 3
```

If bubble population has Orange = 5:

```text
Level validation fails.
```

---

## T19 — Global progress

Start:

```text
0 / 12
```

Resolve one tank.

Expected:

```text
3 / 12
```

Resolve all four target groups.

Expected:

```text
12 / 12
WIN
```

---

## T20 — Win reward idempotency

Win.

Expected:

```text
Gold +20 exactly once
```

Replaying UI animation does not duplicate reward.

---

## T21 — Lose heart idempotency

Lose.

Expected:

```text
Lives -1 exactly once
```

Repeated fail callbacks do not subtract more.

---

## T22 — Double tap

Tap same fish rapidly twice.

Expected:

```text
one routing action only
no duplicate fish
```

---

## T23 — Tap while pile is settling

Expected:

```text
input ignored
```

---

## T24 — Invalid target/bubble mismatch

If target queue requires a FishType not present in sufficient bubble quantity:

```text
level refuses to start
```

---

# 66. DEBUG PANEL

Development build should display:

```text
Level ID
GameState

Global progress:
Collected / Total

Tank 0:
Unlocked?
Target
Fill / 3

Tank 1:
...

Tank 2:
...

Tank 3:
...

Waiting Tray:
Count / 5
Contents

Target queue:
Assigned index
Remaining groups

Bubble pile:
Occupied slots
Empty slots
Queued bubbles

Fish remaining by FishType

Lives
Gold
Score
```

---

# 67. DEBUG COMMANDS

Development-only:

- Restart level.
- Add Gold.
- Set Lives.
- Unlock specific tank.
- Force tank target.
- Fill a tank to 2/3.
- Inject tray fish.
- Set tray to 4/5.
- Pop selected bubble.
- Trigger pile settle.
- Spawn next top bubble.
- Validate level.
- Complete level.

Disable in release.

---

# 68. PERFORMANCE RULES

Mobile-first.

Avoid:

- uncontrolled physics,
- expensive per-frame searches,
- repeated FindObjectOfType,
- unnecessary Update loops,
- high-allocation LINQ inside hot animation/gameplay loops,
- repeated Instantiate/Destroy for frequently reused objects.

Pool:

- FishView.
- BubbleView.
- Bubble pop VFX.
- Small feedback elements.

Gameplay state is event/state driven.

---

# 69. ANIMATION TECHNICAL RULES

Use a tween/animation abstraction.

Do not make domain logic depend on third-party tween callbacks without timeout/recovery handling.

For every async animation sequence:

```text
domain state is authoritative
presentation sequence completes
controller guarantees state exits Resolving
```

Prevent soft-lock if one animation object is missing/destroyed.

---

# 70. FAILURE SAFETY / SOFT-LOCK PREVENTION

Every resolving state must have a guaranteed completion path.

Examples:

```text
Fish route failed visually
→ still reconcile to destination state

Bubble pop VFX missing
→ still remove bubble and settle pile

Tween interrupted
→ snap to authoritative final position
→ continue resolution
```

Never leave:

```text
GameState = Resolving...
```

forever.

---

# 71. ASSET MANIFEST — NEXT PHASE

Before final visual coding, prepare original assets for:

## Tanks

- Tank active empty.
- Tank active with fish/water layer.
- Tank locked/green-plus state.
- Tank shelf/base.
- Hanging target badge frame.
- Hanging badge connector/rings.
- Tank water highlights.
- Unlock animation pieces if separate.

## Waiting Tray

- Five-slot tray background.
- Five individual slot frames.
- Occupied state support.
- Near-full/fail feedback layer.

## Fish

Need one transparent sprite per FishType.

Maintain:

- consistent facing convention,
- readable silhouette,
- consistent scale,
- reference-compatible glossy/cartoon rendering.

## Bubble

- Main transparent bubble.
- Rim/highlight.
- Reflection.
- Bubble shadow/depth layer.
- Bubble pop sprites/VFX.
- Small bubble particles.
- Frozen modifier assets later if needed.

## HUD

- Level panel.
- Gold coin.
- Heart.
- Plus button.
- Settings.
- Global fish progress block.
- No standalone Score display (M12.1: HUD = Level badge | Gold | Hearts).

## Result UI

- Win/reward screen.
- Lose popup.
- Heart break/-1.
- Retry button.
- Continue/claim.
- Reward x2 placeholder if later required.

## Unlock modal

- Tank preview.
- Lock/plus state.
- 600 Gold button.
- Rewarded ad/free button.
- Close button.

---

# 72. OUT OF SCOPE FOR CORE BUILD

Do not implement yet:

- Out-of-Space rescue 900 Gold/ad.
- Fail Offer IAP package.
- Real-money bundle.
- Real rewarded-ad SDK.
- Heart regeneration timer.
- Frozen numbered bubble.
- Bottom booster buttons.
- Level-gated boosters.
- Backend account.
- Cloud save.
- Remote config.
- Analytics backend.
- Procedural level generator.
- Automated puzzle solver.

Prepare clean seams/interfaces only where useful.

---

# 73. DEFINITION OF DONE — CORE BUILD

The core game is ready only if:

- Mobile portrait scene starts.
- 4 TankSlots are visually present.
- 2 tanks start active by default.
- Extra tanks show locked/plus state.
- Active tanks each show independent target fish and `x/3`.
- Player can unlock extra tank for 600 Gold.
- Mock rewarded ad can unlock a tank.
- Player can tap fish inside bubbles.
- Fish matching any active target routes directly to correct tank.
- Nonmatching fish routes to Waiting Tray.
- Matching ambiguity is deterministic.
- Tank completes at exactly 3 fish.
- Tank completion increments global fish progress by 3.
- Tank receives the next target group.
- Waiting Tray has exactly 5 slots.
- Reaching 5 waiting fish immediately loses.
- Lose costs exactly 1 heart.
- Auto-promotion checks all active tanks.
- Auto-promotion can cascade.
- Bubble with fish remaining reflows its internal fish.
- Empty bubble pops.
- Bubble pile falls/slides downward into the gap.
- Pile uses deterministic slot gravity, not gameplay Rigidbody physics.
- New bubbles enter from the top after settling.
- Every standard bubble contains exactly 3 distinct FishTypes.
- Exact fish quantities match all target groups.
- Global progress reaches TotalFishRequired to win.
- Win awards exactly 20 Gold.
- Restart works.
- No duplicate fish from rapid tap.
- No input while resolution/pile motion is active.
- No animation soft-lock.
- Android build launches and is playable.

---

# 74. IMPLEMENTATION ORDER AFTER ASSETS

After this file is approved:

```text
PHASE 1
Asset specification
↓
Create/import assets

PHASE 2
Static Unity layout
↓
Tanks
Target badges
Global progress
Waiting tray
Bubble pile slots

PHASE 3
Domain models
↓
FishType
Tank state
Target queue
Tray state
Level data
Bubble data
Pile layout
Validator

PHASE 4
Core fish routing
↓
Fish tap
Tank matching
Waiting tray
Tank triple
Global progress

PHASE 5
Multi-tank target lifecycle
↓
Next target
Auto-promotion
Tank unlock

PHASE 6
Bubble lifecycle
↓
Internal fish reflow
Empty detection
Pop
Pile settle
Top replenish

PHASE 7
Animation polish
↓
Fish travel
Tank feedback
Tray feedback
Bubble collapse
Bubble spawn

PHASE 8
Progression
↓
Lives
Gold
Score
Local save

PHASE 9
Tests/debug
↓
Android build
```

Do not reverse this order.

---

# 75. PROMPT CONTRACT FOR CURSOR

Before implementing any large feature, Cursor should answer:

```text
1. Which business rules apply?
2. Which domain state changes?
3. Which classes/files are affected?
4. What is the exact state transition?
5. Which animations are presentation-only?
6. What edge cases exist?
7. Which tests will prove correctness?
```

Only then implement.

---

# 76. DO NOT MAKE THESE COMMON MISTAKES

Do not:

```text
- Treat the game as one tank / one current target.
- Hardcode only one target fish.
- Make Waiting Tray capacity 7.
- Allow rescue after the fifth waiting fish.
- Spawn replacement bubble directly in the popped bubble's lower position.
- Use Rigidbody2D collisions as the logical bubble pile.
- Randomize target assignment at runtime.
- Use fish sprite names as IDs.
- Let UI mutate Gold/Lives/Score.
- Allow fish input while bubbles are collapsing.
- Duplicate level completion reward.
- Deduct multiple hearts for one failure.
- Ignore per-type fish population validation.
- Put all logic into GameManager.
```

---

# 77. FINAL LOGIC SUMMARY

```text
4 tank positions.
2 default active.
2 optional unlockable.

Each active tank has:
- its own FishType target,
- its own 0/3 progress.

Tap fish.

If fish matches any active tank:
→ direct to best matching tank.

If not:
→ Waiting Tray.

Waiting Tray:
5 slots.
The fifth waiting fish = immediate loss.

When a tank reaches 3:
→ complete target group.
→ global progress +3.
→ tank draws next target.
→ scan Waiting Tray.
→ matching waiting fish auto-jump into active tanks.
→ cascading completions are possible.

All fish targets come from a deterministic targetGroupQueue.

Every standard bubble contains exactly 3 distinct FishTypes.

Fish quantities across bubbles exactly equal
the quantity required by all target groups.

When a bubble becomes empty:
→ bubble pops.
→ a physical-looking gap appears.
→ bubbles above/nearby slide/fall downward
  using deterministic logical pile slots.
→ chain settles like stacked eggs.
→ only after settling,
  new bubbles may enter from above.

Global collected fish reaches required total:
→ Win.
→ +20 Gold exactly once.

Waiting Tray reaches 5:
→ Lose.
→ -1 Heart exactly once.

Extra tank:
→ 600 Gold
OR
→ rewarded ad.
```

---

# 78. CURRENT VERSION LOCK

This specification version is:

```text
CURSOR.MD v2
Multi-Tank + Bubble-Pile Gravity
```

Do not revert to the previous single-tank / fixed-bubble-slot interpretation.

Next step after user approval:

```text
ASSET SPECIFICATION
```

No production gameplay code should be started until the required asset list, slicing strategy, dimensions, pivots, layering, and animation states are reviewed.
