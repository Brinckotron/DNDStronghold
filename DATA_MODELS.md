# D&D Stronghold Management App - Data Models

## Stronghold

```typescript
interface Stronghold {
  id: string;
  name: string;
  location: string;
  level: number;
  reputation: number;
  currentWeek: number;
  yearsSinceFoundation: number;
  season: "Spring" | "Summer" | "Fall" | "Winter";
  owners: string[];
  treasury: number; // Gold amount
  buildings: Building[];
  npcs: NPC[];
  resources: Resource[];
  journal: JournalEntry[];
  activeMissions: Mission[];
  availableMissions: Mission[];
  weeklyReport: WeeklyReport | null;
}
```

## Resources

```typescript
interface Resource {
  id: string;
  type: ResourceType;
  amount: number;
  weeklyProduction: number;
  weeklyConsumption: number;
  sources: ResourceSource[];
}

enum ResourceType {
  GOLD = "gold",
  FOOD = "food",
  Timber = "Timber",
  STONE = "stone",
  IRON = "iron",
  LUXURY = "luxury",
  SPECIAL = "special"
}

interface ResourceSource {
  sourceType: "building" | "mission" | "event" | "manual";
  sourceId: string;
  amount: number;
  isProduction: boolean; // true for production, false for consumption
}
```

## Buildings

```typescript
interface Building {
  id: string;
  type: BuildingType;
  name: string;
  level: number;
  constructionStatus: "planning" | "under_construction" | "complete";
  constructionProgress: number; // 0-100%
  constructionTimeRemaining: number; // in weeks
  constructionCost: ResourceCost[];
  workerSlots: number;
  assignedWorkers: string[]; // NPC IDs
  baseProduction: ResourceProduction[];
  actualProduction: ResourceProduction[]; // Adjusted based on workers
  baseUpkeep: ResourceCost[];
  actualUpkeep: ResourceCost[]; // Adjusted based on workers
  specialAbilities: SpecialAbility[];
  condition: number; // 0-100%
}

enum BuildingType {
  FARM = "farm",
  WATCHTOWER = "watchtower",
  SMITHY = "smithy",
  LABORATORY = "laboratory",
  CHAPEL = "chapel",
  MINE = "mine",
  BARRACKS = "barracks",
  LIBRARY = "library",
  TRADE_OFFICE = "trade_office",
  STABLES = "stables",
  TAVERN = "tavern",
  MASONS_YARD = "masons_yard",
  WORKSHOP = "workshop",
  GRANARY = "granary"
}

interface ResourceCost {
  resourceType: ResourceType;
  amount: number;
}

interface ResourceProduction {
  resourceType: ResourceType;
  amount: number;
}

interface SpecialAbility {
  type: string;
  description: string;
  effect: any; // Depends on the ability
}
```

## NPCs

```typescript
interface NPC {
  id: string;
  type: NPCType;
  name: string;
  skills: Skill[];
  assignment: {
    type: "building" | "mission" | "unassigned";
    targetId: string | null;
  };
  happiness: number; // 0-100%
  upkeepCost: ResourceCost[];
  specialAbilities: SpecialAbility[];
  experience: number;
  level: number;
}

enum NPCType {
  PEASANT = "peasant",
  LABORER = "laborer",
  FARMER = "farmer",
  MILITIA = "militia",
  SCOUT = "scout",
  ARTISAN = "artisan",
  SCHOLAR = "scholar",
  MERCHANT = "merchant"
}

interface Skill {
  name: string;
  level: number; // 1-5
  description: string;
}
```

## Journal System

```typescript
interface JournalEntry {
  id: string;
  week: number;
  year: number;
  date: string; // For display purposes
  type: JournalEntryType;
  title: string;
  description: string;
  relatedEntities: {
    type: "building" | "npc" | "mission" | "resource";
    id: string;
  }[];
  importance: "low" | "medium" | "high";
}

enum JournalEntryType {
  BUILDING_START = "building_start",
  BUILDING_COMPLETE = "building_complete",
  NPC_RECRUITED = "npc_recruited",
  NPC_ASSIGNED = "npc_assigned",
  RESOURCE_CHANGE = "resource_change",
  EVENT = "event",
  MISSION_START = "mission_start",
  MISSION_COMPLETE = "mission_complete",
  WEEKLY_REPORT = "weekly_report"
}

interface WeeklyReport {
  id: string;
  week: number;
  year: number;
  completedProjects: {
    type: "building" | "mission";
    id: string;
    name: string;
  }[];
  resourceChanges: {
    resourceType: ResourceType;
    previousAmount: number;
    currentAmount: number;
    netChange: number;
    breakdown: {
      source: string;
      amount: number;
    }[];
  }[];
  incomeExpenseSummary: {
    totalIncome: number;
    totalExpenses: number;
    netChange: number;
    breakdown: {
      category: string;
      income: number;
      expenses: number;
    }[];
  };
  npcStatusChanges: {
    npcId: string;
    npcName: string;
    changes: {
      attribute: string;
      oldValue: any;
      newValue: any;
    }[];
  }[];
  upcomingCompletions: {
    type: "building" | "mission";
    id: string;
    name: string;
    weeksRemaining: number;
  }[];
  events: {
    eventId: string;
    title: string;
    summary: string;
  }[];
}
```

## Missions

```typescript
interface Mission {
  id: string;
  name: string;
  description: string;
  requirements: {
    resources: ResourceCost[];
    npcs: {
      type: NPCType | null;
      count: number;
      minSkillLevel?: {
        skillName: string;
        level: number;
      };
    }[];
    buildings?: {
      type: BuildingType;
      minLevel: number;
    }[];
  };
  duration: number; // in weeks
  progress: number; // 0-100%
  weeksRemaining: number;
  status: "available" | "in_progress" | "completed" | "failed";
  successProbability: number; // 0-100%
  rewards: {
    resources: ResourceProduction[];
    reputationGain: number;
    specialRewards?: any[];
  };
  assignedNPCs: string[]; // NPC IDs
}
```

## Events

```typescript
interface Event {
  id: string;
  type: EventType;
  title: string;
  description: string;
  triggerConditions: any; // Depends on event type
  effects: EventEffect[];
  choices?: EventChoice[];
  resolved: boolean;
}

enum EventType {
  WEATHER = "weather",
  VISITOR = "visitor",
  ATTACK = "attack",
  OPPORTUNITY = "opportunity",
  DISASTER = "disaster",
  SPECIAL = "special"
}

interface EventEffect {
  targetType: "resource" | "building" | "npc" | "stronghold";
  targetId: string | null; // null for stronghold-wide effects
  effectType: "modify" | "add" | "remove";
  attribute: string;
  value: any;
  duration: number | null; // null for permanent effects, otherwise in weeks
}

interface EventChoice {
  id: string;
  description: string;
  requirements?: any;
  effects: EventEffect[];
}
```

## UI State

```typescript
interface UIState {
  activeTab: "dashboard" | "buildings" | "npcs" | "resources" | "journal" | "missions";
  selectedBuilding: string | null;
  selectedNPC: string | null;
  selectedMission: string | null;
  selectedJournalEntry: string | null;
  filters: {
    buildings: {
      types: BuildingType[];
      status: ("planning" | "under_construction" | "complete")[];
    };
    npcs: {
      types: NPCType[];
      assignment: ("building" | "mission" | "unassigned")[];
    };
    journal: {
      types: JournalEntryType[];
      importance: ("low" | "medium" | "high")[];
      dateRange: {
        start: number | null;
        end: number | null;
      };
    };
    missions: {
      status: ("available" | "in_progress" | "completed" | "failed")[];
    };
  };
  notifications: Notification[];
}

interface Notification {
  id: string;
  type: "info" | "warning" | "success" | "error";
  message: string;
  read: boolean;
  timestamp: number;
  relatedEntity?: {
    type: "building" | "npc" | "mission" | "resource" | "event";
    id: string;
  };
}
``` 