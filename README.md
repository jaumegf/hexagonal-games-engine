# HexStrategy

Initial scaffolding plus a very small playable console vertical slice for a reusable C#/.NET hex-based turn strategy engine.

The repository still keeps the architecture intentionally small, but it now includes a playable sample game used to validate the boundaries end to end.

## Project Responsibilities

- `engine/HexStrategy.Core`
  Pure engine/domain space. This is where reusable game concepts belong once implemented, such as board primitives, entities, players, game state, turn structures, and rule execution mechanisms.
- `engine/HexStrategy.Application`
  Application layer around the core. It coordinates use cases, command handling, validation flow, and application-facing outcomes.
- `engine/HexStrategy.Session`
  Runtime hosting/session concerns outside logical game state, such as active sessions, slot assignment, and connection-related runtime context.
- `hosts/HexStrategy.Host.Web`
  ASP.NET Core composition root. It wires dependencies and exposes generic backend APIs without serving a specific game's frontend.
- `games/HexStrategy.Game.KingOfTheHill`
  Example game module implementing the sample game's initial setup, move validation, and win condition without placing those rules in the reusable engine.
  Each game project should also carry its own `rules.md` document describing the playable rules of that specific game.
- `tools/HexStrategy.Console.KingOfTheHill`
  Minimal console consumer used to play and inspect the `KingOfTheHill` game without graphics.
- `tools/HexStrategy.Frontend.Web.KingOfTheHill`
  TypeScript + Canvas web tool used to manually exercise the `KingOfTheHill` game visually through the host APIs.
  It runs as its own lightweight web project and lives in `tools/` because it is a game-specific manual exerciser, not reusable engine code and not part of the host composition root.
- `tests/*`
  Test projects grouped by the production area or game they validate.

## Dependency Direction

The intended dependency flow is:

`HexStrategy.Core`
`<- HexStrategy.Application`
`<- HexStrategy.Session`
`<- HexStrategy.Host.Web`

Additional rules:

- Game projects reference reusable engine contracts they need.
- Console and web projects consume application/game modules.
- Core does not reference application, session, web, transport, rendering, or persistence concerns.
- Circular references are intentionally avoided.

## What Belongs In Core

Core should contain reusable, game-agnostic engine mechanisms only.

Examples of appropriate future content:

- board and coordinate concepts
- cells, units, and state primitives
- players as semantic participants
- turn/phase abstractions
- generic rule execution concepts
- domain events

Examples of what does not belong in Core:

- HTTP or SignalR details
- connection IDs or transport state
- local vs remote human distinctions
- rendering/UI code
- filesystem or database persistence
- AI decision-making logic
- rules of any specific game

## What Belongs In Game Projects

Game projects provide policies on top of the reusable engine.

That includes future concerns such as:

- initial game setup
- rule sets
- turn structure choices
- victory or end conditions
- game-specific entities and behaviors

At this stage the sample game implements a deliberately narrow "King of the Hill" slice to prove the boundary.

## Game Vs Session

Game and session are intentionally different concepts.

- Game: the logical state and rules of a match.
- Session: how a match is currently hosted or accessed at runtime.

A saved game should remain conceptually meaningful even if the original network connections, users, or transport state are gone. That runtime information belongs in `HexStrategy.Session`, not in `HexStrategy.Core`.

## Out Of Scope For This Iteration

The following areas are intentionally deferred to later tasks:

- hex coordinate systems and algorithms
- neighbors, range, and distance calculations
- pathfinding
- terrain and movement rules
- combat and fog of war
- AI behavior or decision generation
- persistence and save/load
- authentication
- real-time multiplayer transport
- broader gameplay systems beyond the sample slice

## Review Notes

This scaffolding keeps the important boundaries intact:

- `IGameDefinition` is intentionally minimal and only covers state creation plus command execution.
- The sample game lives outside the engine and plugs into application/console composition without moving its rules into Core.
- Session concerns are kept separate from semantic player identity in Core.
- No speculative persistence, AI, transport, or rendering abstractions were introduced.
