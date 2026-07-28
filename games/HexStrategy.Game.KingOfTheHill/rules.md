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
- visible row sizes from top to bottom are `3-4-5-6-7-6-5-4-3`
- the objective cell is the center hex at coordinate `(0,0)`
- the board is not randomized

## Units

- each player starts with 5 units
- `Player 1` starts with:
  - `1A` at `(-2,3)`
  - `1B` at `(-1,3)`
  - `1C` at `(0,3)`
  - `1D` at `(1,3)`
  - `1E` at `(-1,4)`
- `Player 2` starts with:
  - `2A` at `(2,-3)`
- `2B` at `(1,-3)`
- `2C` at `(0,-3)`
- `2D` at `(-1,-3)`
- `2E` at `(0,-4)`
- units do not attack
- units belong to exactly one player
- each active block has a strength value `S`
- a single unit starts with `S = 1`
- friendly units may merge into larger blocks

## Turn Structure

- players alternate turns
- `Player 1` always starts
- on a turn, the active player may:
  - move one unit to an adjacent hex
  - pass

## Movement Rules

- a move must target a hex inside the board
- a move must target an adjacent hex
- a move into an enemy-occupied hex is not allowed
- a move into a friendly-occupied adjacent hex merges both blocks
- a player may only move their own units
- after a merge, the resulting block strength is the total number of member units
- after a merge, the surviving block reference is the alphabetically lowest member id
- non-surviving member ids stop being direct move references
- moving a block moves all of its combined strength as one military group

## Control And Victory

- a player controls the hill when, at the end of their turn, at least one of their units occupies `(0,0)`
- if a player controls the hill at the end of their turn, they gain `1` control point
- control scores are tracked separately for each player
- the first player to reach `3` control points wins immediately
- once a winner is declared, the game ends immediately

## Console Commands

- `move <unitId> <q> <r>`
- `pass`
- `show`
- `help`

## Current Limitations

- no combat
- no displacement
- no terrain
- no AI
- no persistence
- no networking

These rules apply only to `HexStrategy.Game.KingOfTheHill`. Other game projects in the repository should provide their own separate `rules.md`.
