# HexStrategy.Game.KingOfTheHill Rules

## Overview

This document defines the rules for the specific game implemented in the `HexStrategy.Game.KingOfTheHill` project.

`King of the Hill` is a minimal two-player hex control game used to validate the current engine slice in console and web mode.

## Players

- 2 players
- `Player 1` and `Player 2`
- both players are human-controlled
- the console user enters commands for the active player on each turn

## Board

- fixed custom hex board centered on `(0,0)`
- the main north-to-south row sizes are `4-7-6-9-8-9-8-9-6-7-4`
- the board opens further outward on both the north and south sides to create deeper staging space
- the objective cell is the center hex at coordinate `(0,0)`
- the board is not randomized

## Units

- each player starts with 23 units
- `Player 1` starts with:
  - `1B` at `(-1,4)`
  - `1C` at `(0,4)`
  - `1D` at `(1,4)`
  - `1E` at `(-5,2)`
  - `1F` at `(-4,2)` with `S = 2`
  - `1G` at `(2,2)`
  - `1H` at `(-5,1)`
  - `1I` at `(3,1)`
  - `1J` at `(-3,4)`
  - `1K` at `(-2,5)`
  - `1L` at `(-1,5)`
  - `1M` at `(0,5)`
  - `1N` at `(1,5)`
  - `1O` at `(3,4)`
  - `1P` at `(-2,4)`
  - `1Q` at `(2,4)`
  - `1R` at `(-5,3)`
  - `1S` at `(3,3)`
  - `1T` at `(1,-2)` with `S = 3`
  - `1U` at `(3,2)`
  - `1V` at `(-1,-1)` with `S = 3`
  - `1W` at `(-6,2)`
  - `1X` at `(2,-1)` with `S = 3`
- `Player 2` starts with:
  - `2B` at `(1,-4)`
  - `2C` at `(0,-4)`
  - `2D` at `(-1,-4)`
  - `2E` at `(5,-2)`
  - `2F` at `(4,-2)` with `S = 2`
  - `2G` at `(-2,-2)`
  - `2H` at `(5,-1)`
  - `2I` at `(-3,-1)`
  - `2J` at `(3,-4)`
  - `2K` at `(2,-5)`
  - `2L` at `(1,-5)`
  - `2M` at `(0,-5)`
  - `2N` at `(-1,-5)`
  - `2O` at `(-3,-4)`
  - `2P` at `(2,-4)`
  - `2Q` at `(-2,-4)`
  - `2R` at `(5,-3)`
  - `2S` at `(-2,-3)`
  - `2T` at `(-1,2)` with `S = 3`
  - `2U` at `(-3,-2)`
  - `2V` at `(1,1)` with `S = 3`
  - `2W` at `(6,-2)`
  - `2X` at `(-2,1)` with `S = 3`
- units belong to exactly one player
- each active block has a strength value `S`
- most units start with `S = 1`
- initial `F` blocks start with `S = 2`
- initial `T`, `V`, and `X` defender blocks start with `S = 3`
- the total starting strength per player is `30`
- friendly units may merge into larger blocks

## Turn Structure

- players alternate turns
- `Player 1` always starts
- on a turn, the active player may:
  - move one block up to its allowed movement range
  - pass

## Movement Rules

- a move must target a hex inside the board
- a block with `S = 1` may move up to `2` hexes in one turn
- a block with `S > 1` may move only `1` hex in one turn
- a move must target a hex within the block movement range
- multi-step movement must follow a real traversable path; blocked or occupied intermediate hexes cannot be crossed
- a move into a friendly-occupied adjacent hex merges both blocks
- a move into an enemy-occupied hex is a confrontation
- if the attacking block has greater `S` than the defending block, the defender is eliminated and the attacker moves into the hex
- if both opposing blocks have the same `S`, the move is blocked because neither side can defeat the other
- if the attacking block has lower `S`, the move is also blocked
- a player may only move their own units
- after a merge, the resulting block strength is the total number of member units
- a merge is illegal if it would create a block stronger than `S = 4`
- after a merge, the surviving block reference is the alphabetically lowest member id
- non-surviving member ids stop being direct move references
- moving a block moves all of its combined strength as one military group

## Control And Victory

- entering the center objective may be blocked by adjacent enemy pressure
- when a block tries to enter `(0,0)`, sum the strength of all adjacent enemy blocks around the center
- if that enemy pressure is greater than or equal to the moving block strength, entry to the hill is blocked when the center is empty
- if the center is occupied, it is resolved like a normal confrontation or merge instead of using empty-center pressure blocking
- controlling `r1` does not create any automatic entry or automatic assault on `(0,0)`
- entering `Objective` is always an explicit move decision by the current player
- the AI follows the same rule: it may enter only by choosing a legal move into `(0,0)`
- if the enemy occupies `Objective` and your total adjacent strength in `r1` is strictly greater than the defender's total defense on the hill, siege pressure applies at the end of your turn
- total defense on the hill means the strength on `(0,0)` plus all adjacent friendly strength in `r1`
- siege pressure reduces the defender on `Objective` by `1S`
- if the defender is already `S1`, siege pressure eliminates it and the hill becomes empty
- a player controls the hill when, at the end of their turn, at least one of their units occupies `(0,0)`
- if a player controls the hill at the end of their turn, they gain `1` control point
- control scores are tracked separately for each player
- the match does not end at a fixed score threshold
- after each completed turn, if a player still occupies `(0,0)`, evaluate whether the opponent still has enough remaining strength to ever retake the hill
- if the opponent's total surviving strength is less than or equal to the defender strength currently occupying `(0,0)`, the opponent can no longer retake the hill
- in that case, the player holding the hill wins immediately
- control score can still be used as supporting information or as a future tiebreak rule, but it is not required for the primary victory check

## Console Commands

- `move <unitId> <q> <r>`
- `pass`
- `show`
- `help`

## Current Limitations

- no displacement
- no terrain
- no persistence
- no networking

These rules apply only to `HexStrategy.Game.KingOfTheHill`. Other game projects in the repository should provide their own separate `rules.md`.
