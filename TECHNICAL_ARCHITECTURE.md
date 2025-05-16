# D&D Stronghold Management App - Technical Architecture

## Technology Stack

### Frontend
- **Framework**: React with TypeScript
- **State Management**: Redux Toolkit
- **UI Components**: Material-UI or Tailwind CSS
- **Data Visualization**: Chart.js for resource tracking and statistics
- **Storage**: LocalStorage for saving game state

### Development Tools
- **Package Manager**: npm or yarn
- **Build Tool**: Vite for fast development and optimized production builds
- **Linting**: ESLint with TypeScript rules
- **Testing**: Jest and React Testing Library
- **Version Control**: Git

## Application Architecture

### Core Structure
```
src/
├── assets/            # Static assets like images, icons
├── components/        # Reusable UI components
│   ├── buildings/     # Building-related components
│   ├── npcs/          # NPC-related components
│   ├── resources/     # Resource management components
│   ├── journal/       # Journal and logging components
│   ├── missions/      # Mission management components
│   └── common/        # Common UI elements
├── data/              # Static data definitions
│   ├── buildings.ts   # Building types and properties
│   ├── npcs.ts        # NPC types and properties
│   └── resources.ts   # Resource types and properties
├── hooks/             # Custom React hooks
├── models/            # TypeScript interfaces for data models
├── pages/             # Main application pages
├── services/          # Core game logic services
│   ├── gameState.ts   # Game state management
│   ├── timeSystem.ts  # Time progression logic
│   ├── economy.ts     # Economic calculations
│   ├── events.ts      # Random events generation
│   └── storage.ts     # Save/load functionality
├── store/             # Redux store configuration
│   ├── slices/        # Redux slices for different features
│   └── index.ts       # Store configuration
├── utils/             # Utility functions
└── App.tsx            # Main application component
```

## Key Components

### Game State Management
- **Central Store**: Redux store maintains the complete game state
- **Slices**: Separate slices for buildings, NPCs, resources, journal, etc.
- **Persistence**: Save/load functionality using localStorage
- **Time System**: Logic for advancing time and triggering weekly events

### UI Components

#### Dashboard
- Stronghold overview with key metrics
- Resource summary with visual indicators
- Next Turn button
- Recent events display
- Alerts and notifications

#### Buildings Management
- Building list with filterable categories
- Building detail view with stats and worker assignment
- Construction interface for new buildings
- Upgrade options for existing buildings

#### NPC Management
- NPC roster with filtering options
- NPC detail view with skills and assignment
- Recruitment interface
- Training and skill development options

#### Resource Management
- Resource tracking with production/consumption breakdown
- Visual charts for resource trends
- Manual adjustment controls
- Resource allocation planning

#### Journal System
- Chronological event log
- Weekly report archive
- Filtering and search functionality
- Event detail view

#### Mission System
- Available mission list
- Mission detail view
- NPC and resource assignment interface
- Mission results display

## Data Flow

### Game Loop
1. User views current state of stronghold
2. User makes decisions (build, recruit, assign, etc.)
3. User clicks "Next Turn" button
4. System processes:
   - Resource production and consumption
   - Building construction progress
   - NPC activities and skill improvements
   - Mission progress
   - Random events generation
5. System generates weekly report
6. UI updates to reflect new state
7. Loop repeats

### State Updates
- **Immediate Actions**: Building placement, NPC assignment, resource allocation
- **Turn-Based Updates**: Resource production, construction progress, mission advancement
- **Random Events**: Generated based on stronghold properties and random chance

## Save/Load System

### Save Data Structure
```typescript
interface SaveGame {
  version: string;          // App version for compatibility
  timestamp: number;        // When the save was created
  name: string;             // Save name given by user
  gameState: Stronghold;    // Complete game state
}
```

### Storage Methods
- **Auto-save**: Automatic saving after each turn
- **Manual save**: User-triggered saves with custom names
- **Export/Import**: JSON export/import for backup and sharing

## Calculation Systems

### Resource Production
- Base production values from buildings
- Modified by assigned worker skills
- Further modified by special abilities and events
- Seasonal variations

### Building Construction
- Resource cost verification
- Worker assignment impact on construction speed
- Progress tracking and completion events

### Economy System
- Weekly income calculation from all sources
- Weekly expense calculation for upkeep
- Treasury management with alerts for low funds

### NPC System
- Skill improvement based on assignments
- Happiness factors affecting efficiency
- Upkeep costs and benefits calculation

## Extensibility

### Modding Support
- Data-driven design for easy addition of:
  - New building types
  - New NPC types
  - New resource types
  - New events and missions

### Custom Content
- User-defined stronghold customization
- Optional rule implementations
- Difficulty settings

## Performance Considerations

### Optimization Strategies
- Memoization of expensive calculations
- Efficient state updates with Redux Toolkit
- Virtualized lists for large collections of items
- Lazy loading of non-critical components

### Data Size Management
- Pruning of old journal entries
- Compression of save data
- Efficient storage of game state 