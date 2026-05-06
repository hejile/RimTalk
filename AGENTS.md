# RimAgent Development Guide

This project is a fork of Rimworld mod RimTalk.

## Build & Environment
- **Framework:** .NET Framework 4.8 (target RimWorld 1.5/1.6).
- **Primary Tool:** Use `dotnet build` with `GameVersion` and `BuildingWithScript` properties.
  ```bash
  dotnet build /p:GameVersion=1.6 /p:BuildingWithScript=true
  ```
- **Dependencies:** 
  - Uses `Krafs.Rimworld.Ref` for RimWorld/Unity references by default.
  - Local DLLs in `Libs/`: `Bubbles.dll`, `Scriban.dll`.
- **Output:** Assemblies are placed in `$(GameVersion)/Assemblies/`.

## Architecture & Entrypoints
- **Mod Name:** `RimTalk` (Assembly: `RimAgent.dll`).
- **Core Logic:**
  - `RimTalkSettings`: Main configuration entry point.
  - `TalkService`: Orchestrates pawn dialogue generation.
  - `AIService`: Handles communication with LLM providers (Gemini, OpenAI, etc.).
  - `ScribanParser`: Processes dialogue templates with access to game objects (`pawn`, `map`, `pawns`).
- **Hooks & Extensions:**
  - `ContextHookRegistry`: Use this to register new variables or hooks for Scriban templates.
  - `RimTalkPromptAPI`: Public API for other mods to interact with the prompt system.
- **Harmony Patches:** Located in `Source/Patch/`. Key patches handle `Archive` (logging), `Thought` (mood effects), and `Bubble` (UI display).

## Testing & Verification
- **In-Game Testing:** Required for UI and AI logic.
- **Debug Tools:** `RimTalkDebug` MainButton (accessible via `Source/UI/DebugWindow.cs`) provides an overlay for real-time inspection.
- **Logs:** Use RimWorld's internal logger (`Log.Message`, `Log.Warning`, `Log.Error`).

## Conventions
- **Namespace:** `RimTalk`
- **Asset Paths:** `About/`, `Defs/`, `Languages/`, `Textures/` must be deployed to the RimWorld Mods folder.
- **Scriban Templates:** Follow the format used in `Source/Prompt/Parser/VariableDefinitions.cs`.

## Dependencies (Mod List)
- Harmony
- Interaction Bubbles (Jaxe.Bubbles)
