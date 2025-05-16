# D&D Stronghold Management App - Design Document

## Overview
This application allows Dungeon Masters to manage player strongholds in D&D campaigns. The app tracks resources, buildings, NPCs, economy, and time progression on a weekly basis.

## Core Systems

### Time System
- **Turn-based**: Each turn represents one week of in-game time
- **Next Turn Button**: Advances time and triggers:
  - Resource collection and upkeep
  - Building progress
  - NPC activities
  - Random events
  - End-of-week report generation

### Journal/Log System
- **Event Logging**: Records all significant events not included in end-of-week report:
  - Building/upgrade construction start
  - NPC recruitment/assignment
  - Resource collection/consumption
  - Random events
  - Weekly reports
- **Weekly Report**: Comprehensive summary of:
  - Completed projects and buildings
  - Resource changes
  - Income/expenses
  - NPC status changes
  - Upcoming project completions

### Resource Management
- **Resource Types**:
  - Gold (currency)
  - Food
  - Timber
  - Stone
  - Iron
  - Luxury goods
  - Special materials
- **Resource Tracking**:
  - Current amounts
  - Weekly income/production
  - Weekly consumption/upkeep
  - Manual adjustment option
- **Resource Allocation**:
  - Building/upgrade construction costs
  - NPC upkeep
  - Mission funding

### Building System
- **Construction Process**:
  - Resource cost
  - Construction time (in weeks)
  - Worker requirements
  - Progress tracking
- **Building Properties**:
  - Worker slots (varies by building)
  - Base production values
  - Base upkeep costs
  - Special abilities/bonuses
- **Building Types** (with unique properties):
  - Farm: Food production
  - Watchtower: Security, scouting
  - Smithy: Equipment, iron processing
  - Laboratory: Research, special items
  - Chapel: Morale, healing
  - Mine: Stone/iron production
  - Barracks: Military training, defense
  - Library: Research, knowledge
  - Trade Office: Income, merchant connections
  - Stables: Mounts, transportation
  - Tavern: Income, information, morale
  - Mason's Yard: Stone processing, construction speed
  - Workshop: Crafting, Timber processing
  - Granary: Food storage, reduced spoilage

### NPC Management
- **NPC Types** (with unique skills/abilities):
  - Peasant: Basic labor, low cost
  - Laborer: Construction focus
  - Farmer: Agriculture focus
  - Militia: Basic defense
  - Scout: Exploration, intelligence
  - Artisan: Crafting, quality goods
  - Scholar: Research, special projects
  - Merchant: Trade, income generation
- **NPC Properties**:
  - Skills/proficiencies
  - Upkeep cost
  - Happiness/morale
  - Special abilities
- **NPC Assignment**:
  - Building assignment (fills worker slots)
  - Mission assignment
  - Training options

### Economy System
- **Income Sources**:
  - Building production
  - Trade
  - Special events
  - Missions
- **Expenses**:
  - Building upkeep
  - NPC wages
  - Construction costs
  - Mission costs
- **Economic Factors**:
  - Building efficiency based on worker skills
  - Seasonal effects on production
  - Random events affecting economy
  - Trade opportunities

### Mission/Events System
- **Missions**:
  - Resource requirements
  - NPC requirements
  - Duration
  - Success probability
  - Rewards
- **Random Events**:
  - Weather effects
  - Visitors
  - Attacks/threats
  - Opportunities
  - Disasters

## User Interface

### Main Dashboard
- Stronghold overview
- Resource summary
- Current week/date/season display
- Next Turn button
- Alert notifications

### Buildings Tab
- List of constructed buildings
- Building details view
- Construction options
- Worker assignment interface

### NPCs Tab
- List of all NPCs
- NPC details and stats
- Assignment options
- Recruitment interface

### Resources Tab
- Detailed resource tracking
- Production/consumption breakdown
- Manual adjustment controls
- Projected future resources

### Journal Tab
- Chronological event log
- Weekly reports archive
- Filtering and search options

### Missions Tab
- Available missions
- Active missions
- Mission creation (for DM)
- Mission results

## Data Structure

### Stronghold
- Name
- Location
- Level/size
- Reputation
- Current week/date
- Owner(s)

### Resources
- Type
- Amount
- Weekly production
- Weekly consumption
- Source tracking

### Buildings
- Type
- Level/quality
- Construction status
- Assigned workers
- Production values
- Upkeep costs
- Special abilities

### NPCs
- Type
- Name
- Skills
- Assignment
- Happiness
- Upkeep cost
- Special abilities

### Events
- Type
- Description
- Date/week
- Effects
- Related entities (buildings, NPCs)

### Missions
- Type
- Description
- Requirements
- Duration
- Status
- Rewards

## Technical Implementation Notes

### Data Persistence
- Local storage for saving/loading stronghold data
- Export/import functionality for backup

### Extensibility
- Modular design to allow for:
  - New building types
  - New NPC types
  - Custom resources
  - Custom events

### User Customization
- Custom naming for stronghold, buildings, NPCs
- Adjustable difficulty/economy settings
- Optional rule implementations

## Future Enhancements
- NPC relationships and events
- Seasonal effects on production
- Integration with D&D campaign notes