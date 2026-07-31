# King of the Hill Test Guide

## Purpose

This document explains how the current `HexStrategy.Game.KingOfTheHill.Tests` project is organized and what kinds of behavior it covers.

Right now the test suite was built incrementally while the rules and AI were evolving, so this guide is meant to make the suite readable for further collaboration.

## Main Testing Strategy

The suite is intentionally pragmatic.

It focuses on four areas:

1. initial state and board geometry
2. core rules of movement, merge, attack, and objective control
3. match flow and turn progression
4. selected AI behavior regressions

The tests are not organized into separate files yet. They currently live together in:

- [KingOfTheHillGameMatchTests.cs](C:\Users\Usuario\OneDrive\Projects\Codex\hexagonal-games-engine\tests\HexStrategy.Game.KingOfTheHill.Tests\KingOfTheHillGameMatchTests.cs)

That file is already large enough that splitting it by topic would be a reasonable future cleanup, but for now everything is in one place.

## Current Categories

### 1. Initial State and Board Shape

These tests verify:

- current player at match start
- total number of units
- initial strength distribution
- important starting coordinates
- custom board coordinates that must exist
- board coordinates that must not exist
- row-size profile of the current board shape

Typical examples:

- `StartNew_InitializesExpectedState`
- `StartNew_UsesConfiguredPlayerControllers`

These are high-value regression tests because the board and deployment have changed many times.

### 2. Movement and Geometry Rules

These tests verify:

- legal adjacent movement
- two-step movement for `S1`
- blocked paths through occupied intermediates
- board-specific adjacency expectations
- moves outside movement range
- moves outside the board

Typical examples:

- `Execute_LegalAdjacentMove_Succeeds`
- `Execute_UsesBoardGeometryForAdjacency`
- `Execute_SingleUnitCanMoveTwoHexesInOneTurn`

These are especially important because the board shape is no longer a simple regular-radius hex.

### 3. Merge Rules

These tests verify:

- friendly merge execution
- surviving unit reference identity
- member list preservation
- resulting strength after merge
- merge legality under the maximum block strength cap

Typical examples include tests where:

- a unit merges into another
- the alphabetically lowest surviving id becomes the block reference
- the final block contains the expected member ids

### 4. Combat Rules

These tests verify:

- stronger attacker kills weaker defender
- equal-strength confrontation is blocked
- weaker attacker is rejected
- objective entry blocking by adjacent pressure

Typical examples:

- attack success and elimination
- objective-entry allowed or denied depending on pressure

### 5. Objective and Scoring Rules

These tests verify:

- control point gain when ending a turn on `Objective`
- no control point when not holding `Objective`
- automatic overrun resolution at end of turn
- no overrun when adjacent strength only ties the defender

Typical examples:

- `Execute_Pass_TriggersAutomaticObjectiveAssault_WhenAdjacentStrengthExceedsDefender`
- `Execute_Pass_DoesNotTriggerAutomaticObjectiveAssault_WhenAdjacentStrengthOnlyEqualsDefender`

### 6. Match-End Rule

These tests verify the current end-of-match logic:

- the match does not end at a fixed score target
- the match ends only when the score leader still holds `Objective`
- and the trailing player can no longer exceed the strength currently holding `Objective`

Typical examples:

- `Execute_ScoreLeadDoesNotEndMatch_WhenTrailingPlayerStillHasEnoughStrengthToRetakeObjective`
- `Execute_ScoreLeadEndsMatch_WhenTrailingPlayerLacksEnoughStrengthToRetakeObjective`

This is one of the most important recent rule changes.

### 7. Turn Flow

These tests verify:

- turn order
- turn-number increment timing
- AI pending-turn behavior in the registry
- automated turn execution

Typical examples:

- `Execute_TurnNumberIncrementsOnlyAfterBothPlayersCompleteRound`
- `Registry_LeavesAiReplyPending_AfterHumanTurn`
- `Registry_ExecutesAutomatedTurn_WhenRequested`

### 8. AI Behavior Regression Tests

These tests verify specific tactical decisions that were previously wrong and then corrected.

Current AI-focused tests cover themes such as:

- retreating from `Objective` when death is likely
- preferring reinforcement on `Objective` over pointless retreat
- preferring stronger reserves over weaker distant units
- preferring `r1` overrun setup when it can immediately capture the hill
- preferring stronger inward siege approach heuristics over redundant siege merges

Typical examples:

- `AutomatedDefinition_RetreatsFromObjective_WhenEliminationNextTurnIsLikely`
- `AutomatedDefinition_ObjectiveEmergencyRetreatScore_IsPositive_ForSafeExit`
- `AutomatedDefinition_PrefersObjectiveReinforcement_OverRetreat_WhenHoldCanBePreserved`
- `AutomatedDefinition_PrefersStrongerReserveOverSingleUnit_WhenSiegingObjective`
- `AutomatedDefinition_PrefersR1OverrunSetup_WhenItCanCaptureObjectiveByAdjacentStrength`
- `AutomatedDefinition_Level4SiegeApproachScore_BeatsRedundantSiegeMerge`

These tests are not intended to prove that the AI is "good". They are intended to pin down known tactical expectations and stop regressions.

## Why Some AI Tests Use Reflection

Some tests call internal heuristic functions through reflection.

Reason:

- sometimes we want to verify a specific heuristic score directly
- not just the final full-turn chosen command

This is useful when:

- the full decision stack has many earlier-priority rules
- we only want to validate that one scoring function behaves correctly

This is a practical debugging technique, not a final elegant architecture.

## Current Limitations of the Test Suite

### All tests in one file

This makes the suite easy to start with, but harder to browse now that it has grown.

Natural future split:

- `BoardTests`
- `MovementTests`
- `MergeTests`
- `CombatTests`
- `ObjectiveTests`
- `AiTests`
- `MatchFlowTests`

### AI tests are selective, not exhaustive

The suite does not prove strategic quality over full matches.

It only proves:

- specific situations the AI must not mishandle again

### No frontend tests

The current suite is backend-only.

It does not test:

- canvas rendering
- local storage behavior
- toolbar/debug interactions
- frontend log formatting

Those are currently validated manually through the web tool.

## Recommended Usage During Iteration

When changing rules:

- run the whole test project
- then manually validate a few web-tool matches

When changing AI heuristics:

- add a focused regression test for the exact scenario being corrected
- prefer one concrete board state per bug
- do not rely only on broad "AI vs AI feels better" evaluation

## Current Status

At the time of writing, the suite passes with:

- `35` passing tests
- no skipped tests

That number should be updated if the suite changes significantly.
