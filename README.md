# D&D Stronghold Management App

A comprehensive application for Dungeon Masters to manage player strongholds in D&D campaigns. This app enables tracking resources, buildings, NPCs, economy, and time progression on a weekly basis.

## Project Overview

This application allows players and DMs to:
- Construct and manage buildings in a D&D stronghold
- Recruit and assign NPCs with different skills and abilities
- Track and manage resources (gold, food, Timber, stone, etc.)
- Progress time on a weekly basis with a turn-based system
- Record events in a detailed journal system
- Manage missions and special events
- Track income, upkeep, and the stronghold's economy

## Documentation

This repository contains the following design and planning documents:

- **[DESIGN_DOCUMENT.md](./DESIGN_DOCUMENT.md)** - Comprehensive design overview
- **[DATA_MODELS.md](./DATA_MODELS.md)** - Detailed data structures and models
- **[BUILDING_NPC_DETAILS.md](./BUILDING_NPC_DETAILS.md)** - Specifications for buildings and NPCs
- **[TECHNICAL_ARCHITECTURE.md](./TECHNICAL_ARCHITECTURE.md)** - Technical architecture and stack
- **[UI_MOCKUPS.md](./UI_MOCKUPS.md)** - UI mockups and user interaction flows
- **[IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md)** - Development phases and timeline

## Features

### Core Systems
- **Time System**: Weekly turn-based progression
- **Resource Management**: Track gold, food, Timber, stone, and other resources
- **Building System**: Construct and upgrade various building types
- **NPC Management**: Recruit, assign, and develop different NPC types
- **Journal System**: Record events and generate weekly reports
- **Mission System**: Send NPCs on missions for rewards
- **Economy System**: Track income, expenses, and manage stronghold finances

### Building Types
- Farm, Watchtower, Smithy, Laboratory, Chapel, Mine, Barracks, Library
- Trade Office, Stables, Tavern, Mason's Yard, Workshop, Granary

### NPC Types
- Peasant, Laborer, Farmer, Militia, Scout, Artisan, Scholar, Merchant

## Technology Stack

- **Language**: C# with nullable reference types and implicit usings enabled
- **UI Framework**: Windows Forms
- **Target Framework**: `net8.0-windows` (Windows only)
- **State Management**: Singleton `GameStateService` with a command/invoker pattern for undoable actions
- **Serialization**: `System.Text.Json` (no external NuGet dependencies)
- **Storage**: Saves are written to user-chosen `.stronghold` files via the File menu

The design documents above describe an earlier React/TypeScript plan that was not
pursued; the implemented application is the .NET WinForms app in
`DNDStronghold-WinForms/`.

## Getting Started

### Prerequisites

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Running the App

From the project directory containing `DNDStrongholdApp.csproj`:

```powershell
cd DNDStronghold-WinForms\DNDStrongholdApp
dotnet run
```

### Command-Line Options

The app accepts two optional single-letter flags, handled in `Program.cs`. Because
they are arguments to the app rather than to the `dotnet` CLI, they must be passed
after a `--` separator:

| Command | Effect |
| --- | --- |
| `dotnet run` | Normal start; opens the "Create New Stronghold" flow |
| `dotnet run -- p` | Test mode; loads the pregenerated stronghold from `Data/TestStrongholdData.json` |
| `dotnet run -- d` | Debug mode; shows message-box checkpoints during startup |
| `dotnet run -- p d` | Both of the above |

Flag order does not matter and unrecognized arguments are ignored. The flags are
matched as exact single letters, so `-d`, `--d`, and `--debug` will **not** work.

### Project Structure

| Path | Contents |
| --- | --- |
| `Forms/` | Dialogs for buildings, NPCs, worker assignment, and setup |
| `Models/` | Domain types (`Stronghold`, `Building`, `NPC`, `Resource`, `Mission`, `Journal`) |
| `Services/` | `GameStateService`, `BuildingTypeService`, `BioGeneratorService` |
| `Commands/` | Command objects for save/load and other state changes |
| `Data/` | JSON definitions for buildings, names, bios, and test data |
| `MainDashboard.cs` | Main window and tab layout |

## Development Roadmap

See the [Implementation Plan](./IMPLEMENTATION_PLAN.md) for a detailed development timeline and milestones.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- Created for enhancing D&D gameplay with stronghold management mechanics
- Inspired by various D&D supplements and management games