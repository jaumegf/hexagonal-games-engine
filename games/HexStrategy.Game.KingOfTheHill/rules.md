# HexStrategy.Game.KingOfTheHill Rules

## Overview

This document defines the rules for the specific game implemented in the `HexStrategy.Game.KingOfTheHill` project.

`King of the Hill` is a minimal two-player hex control game used to validate the current engine slice in console mode.

## Players

- 2 players
- `Player 1` and `Player 2`
- both players are human-controlled
- the console user enters commands for the active player on each turn

## Board

- fixed hex board with radius `2`
- the objective cell is the center hex at coordinate `(0,0)`
- the board is not randomized

## Units

- each player starts with 2 units
- `Player 1` starts with `p1a` at `(-2,0)` and `p1b` at `(-2,1)`
- `Player 2` starts with `p2a` at `(2,0)` and `p2b` at `(2,-1)`
- units do not attack
- units do not stack
- units belong to exactly one player

## Turn Structure

- players alternate turns
- `Player 1` always starts
- on a turn, the active player may:
  - move one unit to an adjacent hex
  - pass

## Movement Rules

- a move must target a hex inside the board
- a move must target an adjacent hex
- a move cannot target an occupied hex
- a player may only move their own units

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
