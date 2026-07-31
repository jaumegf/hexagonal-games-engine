# King of the Hill AI Guide

## Purpose

This document explains how the current AI for `HexStrategy.Game.KingOfTheHill` is organized.

The goal is not to describe generic AI theory. The goal is to document the actual decision rules currently implemented, so they can be:

- observed in the frontend log
- discussed using stable identifiers
- reordered
- tuned
- split into finer-grained rules later

Each decision rule has an alphanumeric label. Numeric values increase by `10` so that new rules can be inserted later between existing ones.

## Current Profiles

- `IA4`
  - reference AI profile
  - currently the only active strategic profile
  - all automated play is intentionally normalized to this ruleset while the AI is still being matured

Scaffolding note:

- `IA1`
- `IA2`
- `IA3`

These identifiers are preserved only as compatibility scaffolding for older saved setups, logs, or API inputs.

At the moment they are intentionally routed to the same strategic behavior as `IA4`.

## Log Format

Automated turns are logged in this format:

`Player 2 (IA4) [KH-110] IA4 strong siege approach -> move:q=0,r=-2,unitId=2A`

Structure:

- player and AI level
- decision rule code
- decision rule name
- chosen command

## Match Phases

The AI currently recognizes these internal match phases:

- `Opening`
  - `TurnNumber <= 4`
  - and `Objective` is still empty
- `Midgame`
  - after the opening
  - and either `Objective` is still empty
  - or `Objective` is occupied but both players still have more than `10` total surviving strength
- `Endgame`
  - `Objective` is occupied
  - and at least one player has `10` or less total surviving strength

At the moment, only selected rules depend explicitly on the phase system.

## Decision Families And Phase Bias

Rule order still matters first.

`PhaseBias` does not replace the ordered rule flow. It only adjusts the internal score used when several legal commands are competing inside the same rule family.

Current families:

- `Objective`
- `Siege`
- `Defender`
- `Survival`
- `Merge`
- `Tactical`
- `Fallback`

Applied phase bias table:

| Family | Opening | Midgame | Endgame |
| --- | ---: | ---: | ---: |
| `Objective` | `0` | `+6000` | `+14000` |
| `Siege` | `-4000` | `+8000` | `+6000` |
| `Defender` | `+12000` | `0` | `-8000` |
| `Survival` | `0` | `0` | `+4000` |
| `Merge` | `-2000` | `+4000` | `+2000` |
| `Tactical` | `0` | `+2000` | `0` |
| `Fallback` | `0` | `0` | `0` |

Interpretation:

- `Opening`
  - defenders are strongly favored
  - siege and merge are softened so the AI does not rush inward too early
- `Midgame`
  - siege becomes the main strategic push
  - merge and tactical pressure gain value
- `Endgame`
  - direct objective play becomes dominant
  - survival improves
  - defender-specific behavior loses importance

## Decision Order

The AI evaluates legal commands first, then applies the following ordered rule selection flow.

### `KH-010` Objective reinforcement

Use a move that directly reinforces a friendly block already holding `Objective`.

Typical intent:

- add strength on the hill
- preserve the hold
- reduce the chance of immediate expulsion

### `KH-020` Objective support approach

Bring support closer to a friendly block already holding `Objective`, without abandoning the hold.

Typical intent:

- shorten reinforcement time
- prepare future merges into `Objective`

### `KH-030` Objective emergency retreat

Retreat from `Objective` if staying would likely result in elimination and a safer exit exists.

Typical intent:

- save the unit instead of losing it on the hill

### `KH-040` Preserve solo objective holder

Special case: if the AI has exactly one unit and it already holds `Objective`, prefer `pass`.

### `KH-050` Keep holding Objective

Choose any accepted move or pass that preserves occupation of `Objective` when the hold is not clearly lost.

### `KH-055` IA4 defender reset

`IA4`-only preference.

This rule brings a defender back onto its `r2` anchor after a forward interception, as long as the hill is still empty and there is no stronger offensive or siege action to take first.

Typical intent:

- intercept an early intrusion into `r1`
- then restore the defender to its corridor
- avoid drifting from lane control into premature hill occupation
- but only after higher-priority hill pressure has already been considered

### `KH-065` Objective entry timing

Decide whether a legal entry into `Objective` is worth taking now.

The AI uses this only in `Endgame`, and only if the resulting occupation looks stable enough or materially favorable enough.

Typical factors:

- whether the opponent can still exceed the new objective strength
- whether friendly `r1` support remains around the hill after entry
- whether the entry would expose the new holder to an immediate recapture

### `KH-070` Objective assault posture

Improve the siege posture around `Objective` without entering automatically.

This rule now values moves that:

- improve adjacent pressure in `r1`
- reduce the deficit against the current defender on `Objective`
- prepare a later explicit entry into the hill

Typical intent:

- move into `r1`
- raise adjacent strength above the defender

### `KH-080` Objective reserve mobilization

Move deeper reserves inward during a siege when current contest strength is still insufficient.

Typical intent:

- activate stronger distant blocks
- reduce time-to-center

### `KH-085` Defender intercept

If an active defender has a legal kill from `r2` into an adjacent enemy standing on `r1`, take it immediately.

Typical intent:

- punish an enemy that slips through the defensive lane
- preserve the main tactical purpose of the defender role
- prioritize lane denial before broader siege planning

### `KH-088` Defender lane denial

During the early opening, if an active defender can eliminate an outer-lane enemy whose presence is already building meaningful siege pressure toward the Hill, take that kill before ordinary siege buildup.

Typical intent:

- stop flank pieces before they merge into an inward block
- deny easy early access from `r3` into the inner approach
- preserve the defensive corridor before falling back to generic siege rules

### `KH-090` Objective siege search

Use the search layer for siege situations.

Typical intent:

- inspect a short tactical continuation
- improve choice quality when `Objective` is contested

### `KH-100` Objective breakthrough approach

Advance a block that can become a direct breakthrough piece against the current `Objective` defender.

Typical intent:

- move a block whose strength is already enough to beat the defender

### `KH-110` IA4 strong siege approach

`IA4`-only preference.

This rule favors inward approaches that:

- move a stronger block toward `r1` or `r2`
- improve near-term contest capacity
- create useful follow-up support
- avoid wasting tempo on weaker alternatives

This rule was added to improve siege buildup on larger boards.

### `KH-120` Objective siege merge

Choose a merge that improves siege capacity against an enemy on `Objective`.

Typical intent:

- increase contest strength
- create a future breakthrough block

This rule now includes an excessive-merge penalty so the AI is less likely to over-merge when the added strength is not actually useful.

### `KH-130` Objective siege approach

General inward approach for siege states.

Typical intent:

- move closer
- create future merge partners
- improve pressure without necessarily merging immediately

### `KH-140` Survival retreat

Retreat a threatened unit if the move reduces next-turn elimination risk.

### `KH-150` Inner-ring kill

Prefer a favorable kill that happens on the same ring or a more interior ring relative to the center.

Additional priority:

- if a unit already stands on `r1`, a legal kill from `r1` is strongly preferred
- exception: that `r1` kill is suppressed if the resulting unit would end adjacent to an enemy `S4`

### `KH-160` Forced inner threat

Create a threat inside `r1` or `r2` even if it is not an immediate kill.

Typical intent:

- force the opponent to respond
- improve control of the inner zone

### `KH-170` Defensive merge

Prefer a merge that reduces immediate tactical vulnerability of threatened friendly material.

Restriction:

- defenders (`T`, `V`, `X`) are not reinforced by AI merges

## Defender Role

The `T`, `V`, and `X` blocks begin each match with a defender role.

Defender behavior:

- they prioritize controlling the `r2` axis
- if an enemy steps onto adjacent `r1`, a legal defender intercept is mandatory
- a defender kill that stays on `r2` is strongly preferred
- if a defender is threatened on `r2` by an adjacent enemy block of `S4` or higher, it immediately loses defender status and may be reused for siege pressure into `r1`
- defenders do not voluntarily enter `r1`
- defenders may only break that rule for an immediate hill emergency or an immediate win
- defenders are not reinforced by AI merges
- once a defender commits inward to `r1` or `Objective`, it permanently loses defender status for the rest of the match
- moving between `r2` and `r3` does not remove defender status
- defender coverage is only considered viable while at least `2` defenders remain active
- if a role loss or elimination would leave only `1` active defender, that last defender also loses defender status automatically

### `KH-180` Outer safe kill

Take a favorable kill on an outer ring only if it does not leave the inner rings strategically inferior.

### `KH-190` Distance-1 favorable merge

Probabilistic merge rule for favorable merges close to the center.

Profile dependence:

- `IA4`: always allowed
- lower AIs: reduced probability

### `KH-200` Distance-2 favorable merge

Probabilistic merge rule for favorable merges slightly farther out.

Profile dependence:

- `IA4`: always allowed
- lower AIs: reduced or zero probability

### `KH-210` Second-choice variance

Optional variance layer that can intentionally choose the second-best ranked command.

Currently the configured probability is `0` for all present profiles, so this rule is effectively dormant.

### `KH-220` Ranked fallback

Fallback to the highest preview-ranked command when no earlier labeled rule claims the turn.

### `KH-900` No legal move fallback

Emergency fallback if no legal move exists.

Current behavior:

- choose `pass`

## Important Notes

### Preview score vs rule selection

The AI has two layers:

1. preview scoring of legal commands
2. ordered rule interception

This means:

- a move can have a strong raw preview score
- but still lose to an earlier explicit decision rule

When discussing AI behavior, it is important to distinguish:

- "the move had a high score"
- from
- "an earlier rule preempted it"

### IA4-specific experimentation

At the moment, `IA4` is the best place to experiment with:

- siege timing
- inward mobilization
- anti-overmerge behavior
- objective pressure planning

After the behavior is stable, the same ideas can be selectively downgraded into `IA3`, `IA2`, and `IA1`.
