# HexStrategy.Game.KingOfTheHill Rules

## Overview

This document defines the current playable rules for the sample game implemented in `HexStrategy.Game.KingOfTheHill`.

`King of the Hill` is a two-player hex wargame-style prototype focused on:

- lane control
- merges
- defensive anchors
- staged access to the Hill

## Board

- fixed custom hex board centered on `(0,0)`
- the center hex `(0,0)` is the `Hill`
- the ring around the Hill is `r1`
- rings farther away are `r2`, `r3`, and so on

## Players

- 2 players: `Player 1` and `Player 2`
- the tooling currently supports `Human` and `IA4`

## Units And Roles

Every block has a strength value `S`.

There are four functional roles:

### Single

- strength `S1`
- may move up to `2` hexes
- may enter `r1`
- may enter the `Hill` at `(0,0)`
- this is the only role that can win the match

### Double

- strength `S2`
- may move up to `1` hex
- may enter `r1`
- may not enter the `Hill`
- used to block, escort, and eliminate `Singles`

### Defender

- fixed strength `S3`
- currently represented by the seeded defender identities `T`, `V`, and `X`
- may move up to `1` hex
- may not enter `r1`
- may not enter the `Hill`
- may not merge
- used to hold outer defensive lanes and deny access

### Attacker

- strength `S3` created by merging non-defender blocks
- may move up to `1` hex
- may not enter `r1`
- may not enter the `Hill`
- may not merge any further because the block cap is already reached
- used to pressure and eliminate enemy `Singles` and `Doubles`

## Maximum Block Strength

- the maximum legal block strength is `S3`
- any merge that would create `S4` or higher is illegal

## Movement

- a move must stay inside the board
- a block must move to a different hex
- multi-step movement for `Singles` must follow a real traversable path
- occupied intermediate hexes cannot be crossed
- enemy blocks block traversal
- friendly blocks also block traversal; they may only be used as the final merge destination

## Combat

- a move into an enemy-occupied hex is an attack
- the attack succeeds only if the attacker strength is strictly greater than the defender strength
- on success, the defender is eliminated and the attacker occupies the hex
- if strengths are equal, the move is blocked
- if the attacker is weaker, the move is blocked

## Merging

- a move into a friendly-occupied hex merges both blocks
- only non-defender blocks may merge
- the resulting strength is the total number of merged member units
- the merge is illegal if it would exceed `S3`
- after a merge, the surviving direct reference is the alphabetically lowest member id

## Role Access By Ring

- `Single`
  - may enter `r1`
  - may enter `r0`
- `Double`
  - may enter `r1`
  - may not enter `r0`
- `Defender`
  - may stay at `r2` or farther
  - may not enter `r1`
  - may not enter `r0`
- `Attacker`
  - may stay at `r2` or farther
  - may not enter `r1`
  - may not enter `r0`

## Victory

- the first `Single` that legally enters the `Hill` at `(0,0)` wins immediately
- there is no control-score victory condition
- there is no siege-pressure victory condition
- entering the Hill is always an explicit move decision by the active player or the AI

## Starting Position

The current board and initial setup are intentionally handcrafted for iteration.

### Player 1

- `1B` at `(-1,4)` as `S1`
- `1C` at `(0,4)` as `S1`
- `1D` at `(1,4)` as `S1`
- `1E` at `(-5,2)` as `S1`
- `1F` at `(-4,2)` as `S2`
- `1G` at `(2,2)` as `S1`
- `1H` at `(-5,1)` as `S1`
- `1I` at `(3,1)` as `S1`
- `1J` at `(-3,4)` as `S1`
- `1K` at `(-2,5)` as `S1`
- `1L` at `(-1,5)` as `S1`
- `1M` at `(0,5)` as `S1`
- `1N` at `(1,5)` as `S1`
- `1O` at `(3,4)` as `S1`
- `1P` at `(-2,4)` as `S1`
- `1Q` at `(2,4)` as `S1`
- `1R` at `(-5,3)` as `S1`
- `1S` at `(3,3)` as `S1`
- `1T` at `(1,-2)` as `Defender / S3`
- `1U` at `(3,2)` as `S1`
- `1V` at `(-1,-1)` as `Defender / S3`
- `1W` at `(-6,2)` as `S1`
- `1X` at `(2,-1)` as `Defender / S3`

### Player 2

- `2B` at `(1,-4)` as `S1`
- `2C` at `(0,-4)` as `S1`
- `2D` at `(-1,-4)` as `S1`
- `2E` at `(5,-2)` as `S1`
- `2F` at `(4,-2)` as `S2`
- `2G` at `(-2,-2)` as `S1`
- `2H` at `(5,-1)` as `S1`
- `2I` at `(-3,-1)` as `S1`
- `2J` at `(3,-4)` as `S1`
- `2K` at `(2,-5)` as `S1`
- `2L` at `(1,-5)` as `S1`
- `2M` at `(0,-5)` as `S1`
- `2N` at `(-1,-5)` as `S1`
- `2O` at `(-3,-4)` as `S1`
- `2P` at `(2,-4)` as `S1`
- `2Q` at `(-2,-4)` as `S1`
- `2R` at `(5,-3)` as `S1`
- `2S` at `(-2,-3)` as `S1`
- `2T` at `(-1,2)` as `Defender / S3`
- `2U` at `(-3,-2)` as `S1`
- `2V` at `(1,1)` as `Defender / S3`
- `2W` at `(6,-2)` as `S1`
- `2X` at `(-2,1)` as `Defender / S3`

## Current Intent

The present rule set is designed to create:

- a real outer staging phase
- defender lanes that delay trivial access to the Hill
- tactical value for `Singles`, `Doubles`, `Defenders`, and merged `Attackers`
- a clearer distinction between screening, blocking, escorting, and the final climb onto the Hill

## Out Of Scope For This Slice

- ranged units
- unit production
- terrain effects
- fog of war
- persistence
- networking
- polished production visuals
