# D&D Stronghold Management App - UI Mockups & Interactions

## Main Dashboard

```
+------------------------------------------------------+
|  STRONGHOLD NAME                       Week: 12      |
|  Level: 2  |  Reputation: Respected  |  Season: Fall |
+------------------------------------------------------+
|                                                      |
|  +------------+  +------------+  +------------+      |
|  | RESOURCES  |  | BUILDINGS  |  |    NPCs    |      |
|  |            |  |            |  |            |      |
|  | Gold: 450  |  | Complete: 7|  | Total: 12  |      |
|  | Food: 120  |  | Building: 2|  | Idle: 3    |      |
|  | Timber: 75 |  |            |  |            |      |
|  | Stone: 40  |  |            |  |            |      |
|  +------------+  +------------+  +------------+      |
|                                                      |
|  +-------------------------------------------+       |
|  | RECENT EVENTS                             |       |
|  |                                           |       |
|  | • Farm construction completed             |       |
|  | • Recruited 2 new peasants                |       |
|  | • Heavy rainfall affected food production |       |
|  |                                           |       |
|  +-------------------------------------------+       |
|                                                      |
|  +-------------------------------------------+       |
|  | ALERTS                                    |       |
|  |                                           |       |
|  | ! Low food reserves                       |       |
|  | ! Trade opportunity available             |       |
|  |                                           |       |
|  +-------------------------------------------+       |
|                                                      |
|                [ NEXT TURN ]                         |
|                                                      |
+------------------------------------------------------+
```

### Dashboard Interactions
- **Resource Cards**: Click to open detailed resource management view
- **Building Card**: Click to open building management view
- **NPC Card**: Click to open NPC management view
- **Recent Events**: Click on event to see details in journal
- **Alerts**: Click on alert for more information or to take action
- **Next Turn Button**: Click to advance time by one week

## Buildings Tab

```
+------------------------------------------------------+
| BUILDINGS                        [ + NEW BUILDING ]  |
+------------------------------------------------------+
| FILTERS: [ All ▼ ] [ Complete ▼ ] [ Sort by: Name ▼ ]|
+------------------------------------------------------+
|                                                      |
| +--------------------------------------------------+ |
| | Farm                                   COMPLETE  | |
| | Level: 1  |  Workers: 2/3  |  Condition: 100%    | |
| |                                                  | |
| | Production: 20 Food/week                         | |
| | Upkeep: 5 Gold/week                              | |
| |                                                  | |
| | [ DETAILS ] [ ASSIGN WORKERS ] [ UPGRADE ]       | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | Smithy                             CONSTRUCTING  | |
| | Progress: 75%  |  Time remaining: 1 week         | |
| |                                                  | |
| | Workers assigned: 2 Laborers                     | |
| |                                                  | |
| | [ DETAILS ] [ ASSIGN WORKERS ] [ CANCEL ]        | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | Barracks                               PLANNING  | |
| | Cost: 80 Gold, 30 Timber, 40 Stone               | |
| | Construction time: 4 weeks                       | |
| |                                                  | |
| | [ DETAILS ] [ START CONSTRUCTION ] [ DELETE ]    | |
| +--------------------------------------------------+ |
|                                                      |
+------------------------------------------------------+
```

### Building Detail View

```
+------------------------------------------------------+
| FARM                                      [ BACK ]   |
+------------------------------------------------------+
|                                                      |
| Level: 1  |  Condition: 100%  |  Built: Week 8       |
|                                                      |
| Description: Produces food for your stronghold.      |
|                                                      |
| +------------------+  +------------------+           |
| | PRODUCTION       |  | UPKEEP           |           |
| |                  |  |                  |           |
| | Base: 20 Food    |  | 5 Gold/week      |           |
| | Actual: 25 Food  |  |                  |           |
| +------------------+  +------------------+           |
|                                                      |
| +--------------------------------------------------+ |
| | ASSIGNED WORKERS                     [ ASSIGN ]  | |
| |                                                  | |
| | • John (Farmer) - 150% efficiency                | |
| | • Mary (Peasant) - 100% efficiency               | |
| | • [Empty Slot]                                   | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | SPECIAL ABILITIES                                | |
| |                                                  | |
| | • [Locked] Crop Rotation - Unlocks at Level 2    | |
| | • [Locked] Irrigation - Unlocks at Level 3       | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | UPGRADE TO LEVEL 2                               | |
| |                                                  | |
| | Cost: 30 Gold, 20 Timber, 10 Stone               | |
| | Time: 1 week                                     | |
| |                                                  | |
| | [ UPGRADE ]                                      | |
| +--------------------------------------------------+ |
|                                                      |
+------------------------------------------------------+
```

### New Building Selection

```
+------------------------------------------------------+
| SELECT BUILDING TYPE                     [ BACK ]    |
+------------------------------------------------------+
| FILTERS: [ All ▼ ] [ Production ▼ ] [ Military ▼ ]   |
+------------------------------------------------------+
|                                                      |
| +------------------+  +------------------+           |
| | FARM             |  | WATCHTOWER       |           |
| |                  |  |                  |           |
| | Food Production  |  | Security         |           |
| | Cost: 50G, 30W,  |  | Cost: 40G, 20W,  |           |
| | 10S              |  | 30S              |           |
| |                  |  |                  |           |
| | [ SELECT ]       |  | [ SELECT ]       |           |
| +------------------+  +------------------+           |
|                                                      |
| +------------------+  +------------------+           |
| | SMITHY           |  | BARRACKS         |           |
| |                  |  |                  |           |
| | Tool Production  |  | Military         |           |
| | Cost: 80G, 30W,  |  | Cost: 80G, 30W,  |           |
| | 40S, 20I         |  | 40S              |           |
| |                  |  |                  |           |
| | [ SELECT ]       |  | [ SELECT ]       |           |
| +------------------+  +------------------+           |
|                                                      |
+------------------------------------------------------+
```

## NPCs Tab

```
+------------------------------------------------------+
| NPCs                                [ + RECRUIT ]    |
+------------------------------------------------------+
| FILTERS: [ All ▼ ] [ Unassigned ▼ ] [ Sort: Name ▼ ] |
+------------------------------------------------------+
|                                                      |
| +--------------------------------------------------+ |
| | John (Farmer)                                    | |
| | Level: 2  |  Happiness: 85%  |  Assigned: Farm   | |
| |                                                  | |
| | Skills: Farming (3), Animal Handling (2)         | |
| | Upkeep: 3 Gold, 1 Food per week                  | |
| |                                                  | |
| | [ DETAILS ] [ REASSIGN ] [ TRAIN ]               | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | Emma (Scholar)                                   | |
| | Level: 3  |  Happiness: 90%  |  Assigned: Library| |
| |                                                  | |
| | Skills: Research (3), Lore (3), Medicine (2)     | |
| | Upkeep: 6 Gold, 1 Food per week                  | |
| |                                                  | |
| | [ DETAILS ] [ REASSIGN ] [ TRAIN ]               | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | Robert (Peasant)                                 | |
| | Level: 1  |  Happiness: 70%  |  Unassigned       | |
| |                                                  | |
| | Skills: Labor (1), Survival (1)                  | |
| | Upkeep: 2 Gold, 1 Food per week                  | |
| |                                                  | |
| | [ DETAILS ] [ ASSIGN ] [ TRAIN ]                 | |
| +--------------------------------------------------+ |
|                                                      |
+------------------------------------------------------+
```

### NPC Detail View

```
+------------------------------------------------------+
| JOHN (FARMER)                             [ BACK ]   |
+------------------------------------------------------+
|                                                      |
| Level: 2  |  Experience: 45/100  |  Happiness: 85%   |
|                                                      |
| Currently assigned to: Farm                          |
|                                                      |
| +--------------------------------------------------+ |
| | SKILLS                                           | |
| |                                                  | |
| | • Farming: Level 3 - Expert crop cultivation     | |
| | • Animal Handling: Level 2 - Livestock care      | |
| | • Herbalism: Level 1 - Basic plant knowledge     | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | UPKEEP                                           | |
| |                                                  | |
| | • 3 Gold per week                                | |
| | • 1 Food per week                                | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | HAPPINESS FACTORS                                | |
| |                                                  | |
| | • +10% Assigned to preferred building            | |
| | • +5% Well-fed                                   | |
| | • -5% No tavern available                        | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
| [ REASSIGN ] [ TRAIN SKILLS ] [ DISMISS ]            |
|                                                      |
+------------------------------------------------------+
```

### Recruitment Interface

```
+------------------------------------------------------+
| RECRUIT NPCs                              [ BACK ]   |
+------------------------------------------------------+
| Available Gold: 450                                  |
+------------------------------------------------------+
|                                                      |
| +------------------+  +------------------+           |
| | PEASANT          |  | LABORER          |           |
| |                  |  |                  |           |
| | Basic worker     |  | Construction     |           |
| | Cost: 10 Gold    |  | Cost: 20 Gold    |           |
| | Upkeep: 2G, 1F   |  | Upkeep: 3G, 2F   |           |
| |                  |  |                  |           |
| | [ RECRUIT ]      |  | [ RECRUIT ]      |           |
| +------------------+  +------------------+           |
|                                                      |
| +------------------+  +------------------+           |
| | FARMER           |  | MILITIA          |           |
| |                  |  |                  |           |
| | Agriculture      |  | Defense          |           |
| | Cost: 20 Gold    |  | Cost: 25 Gold    |           |
| | Upkeep: 3G, 1F   |  | Upkeep: 4G, 2F   |           |
| |                  |  |                  |           |
| | [ RECRUIT ]      |  | [ RECRUIT ]      |           |
| +------------------+  +------------------+           |
|                                                      |
+------------------------------------------------------+
```

## Resources Tab

```
+------------------------------------------------------+
| RESOURCES                                            |
+------------------------------------------------------+
|                                                      |
| +--------------------------------------------------+ |
| | GOLD                                             | |
| |                                                  | |
| | Current: 450                                     | |
| | Income: +35 per week                             | |
| | Expenses: -25 per week                           | |
| | Net: +10 per week                                | |
| |                                                  | |
| | [Chart showing gold over time]                   | |
| |                                                  | |
| | [ DETAILS ] [ ADJUST ]                           | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | FOOD                                             | |
| |                                                  | |
| | Current: 120                                     | |
| | Production: +25 per week                         | |
| | Consumption: -15 per week                        | |
| | Net: +10 per week                                | |
| |                                                  | |
| | [Chart showing food over time]                   | |
| |                                                  | |
| | [ DETAILS ] [ ADJUST ]                           | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | Timber                                           | |
| |                                                  | |
| | Current: 75                                      | |
| | Production: +0 per week                          | |
| | Consumption: -5 per week                         | |
| | Net: -5 per week                                 | |
| |                                                  | |
| | [Chart showing Timber over time]                 | |
| |                                                  | |
| | [ DETAILS ] [ ADJUST ]                           | |
| +--------------------------------------------------+ |
|                                                      |
+------------------------------------------------------+
```

### Resource Detail View

```
+------------------------------------------------------+
| FOOD RESOURCES                            [ BACK ]   |
+------------------------------------------------------+
|                                                      |
| Current amount: 120                                  |
| Storage capacity: 200                                |
|                                                      |
| [Chart showing food levels over past 20 weeks]       |
|                                                      |
| +--------------------------------------------------+ |
| | PRODUCTION                                       | |
| |                                                  | |
| | • Farm: +25 per week                             | |
| | • Hunting Mission: +5 per week                   | |
| | • Trading: +0 per week                           | |
| |                                                  | |
| | Total: +30 per week                              | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | CONSUMPTION                                      | |
| |                                                  | |
| | • NPC Upkeep: -15 per week                       | |
| | • Building Upkeep: -5 per week                   | |
| |                                                  | |
| | Total: -20 per week                              | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | ADJUST RESOURCES                                 | |
| |                                                  | |
| | Amount: [____]  Reason: [________________]       | |
| |                                                  | |
| | [ ADD ] [ REMOVE ]                               | |
| +--------------------------------------------------+ |
|                                                      |
+------------------------------------------------------+
```

## Journal Tab

```
+------------------------------------------------------+
| JOURNAL                                              |
+------------------------------------------------------+
| FILTERS: [ All ▼ ] [ Buildings ▼ ] [ Week: 1-12 ▼ ]  |
+------------------------------------------------------+
|                                                      |
| +--------------------------------------------------+ |
| | WEEKLY REPORT - WEEK 12                          | |
| | Fall, Year 1                                     | |
| |                                                  | |
| | [Summary of week's events and changes]           | |
| |                                                  | |
| | [ VIEW DETAILS ]                                 | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | SMITHY CONSTRUCTION - WEEK 12                    | |
| | Construction progress: 75%                       | |
| | Time remaining: 1 week                           | |
| |                                                  | |
| | [ VIEW DETAILS ]                                 | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | HEAVY RAINFALL - WEEK 11                         | |
| | Food production reduced by 20% this week         | |
| |                                                  | |
| | [ VIEW DETAILS ]                                 | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | RECRUITED 2 PEASANTS - WEEK 11                   | |
| | Total cost: 20 Gold                              | |
| | Weekly upkeep: +4 Gold, +2 Food                  | |
| |                                                  | |
| | [ VIEW DETAILS ]                                 | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | FARM COMPLETED - WEEK 10                         | |
| | Production: +20 Food per week                    | |
| | Upkeep: +5 Gold per week                         | |
| |                                                  | |
| | [ VIEW DETAILS ]                                 | |
| +--------------------------------------------------+ |
|                                                      |
+------------------------------------------------------+
```

### Weekly Report View

```
+------------------------------------------------------+
| WEEKLY REPORT - WEEK 12                    [ BACK ]  |
| Fall, Year 1                                         |
+------------------------------------------------------+
|                                                      |
| +--------------------------------------------------+ |
| | COMPLETED PROJECTS                               | |
| |                                                  | |
| | • None this week                                 | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | RESOURCE CHANGES                                 | |
| |                                                  | |
| | • Gold: 440 → 450 (+10)                          | |
| | • Food: 110 → 120 (+10)                          | |
| | • Timber: 80 → 75 (-5)                           | |
| | • Stone: 40 → 40 (0)                             | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | INCOME & EXPENSES                                | |
| |                                                  | |
| | Income:                                          | |
| | • Trade Office: +15 Gold                         | |
| | • Tavern: +10 Gold                               | |
| | • Missions: +10 Gold                             | |
| |                                                  | |
| | Expenses:                                        | |
| | • Building upkeep: -15 Gold                      | |
| | • NPC wages: -10 Gold                            | |
| |                                                  | |
| | Net: +10 Gold                                    | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | NPC STATUS CHANGES                               | |
| |                                                  | |
| | • John (Farmer): Farming skill improved (2→3)    | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | UPCOMING COMPLETIONS                             | |
| |                                                  | |
| | • Smithy: 1 week remaining                       | |
| | • Hunting Mission: 2 weeks remaining             | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
+------------------------------------------------------+
```

## Missions Tab

```
+------------------------------------------------------+
| MISSIONS                            [ + NEW MISSION ]|
+------------------------------------------------------+
| FILTERS: [ All ▼ ] [ In Progress ▼ ] [ Sort: Time ▼ ]|
+------------------------------------------------------+
|                                                      |
| +--------------------------------------------------+ |
| | HUNTING EXPEDITION                  IN PROGRESS  | |
| | Time remaining: 2 weeks  |  Success chance: 85%  | |
| |                                                  | |
| | Assigned: 2 Scouts                               | |
| | Rewards: +15 Food, +5 Leather                    | |
| |                                                  | |
| | [ DETAILS ] [ RECALL ]                           | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | BANDIT CAMP RAID                      AVAILABLE  | |
| | Duration: 3 weeks  |  Success chance: 70%        | |
| |                                                  | |
| | Requirements: 3 Militia, 1 Scout                 | |
| | Rewards: +50 Gold, +10 Reputation                | |
| |                                                  | |
| | [ DETAILS ] [ START MISSION ]                    | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | TRADE CARAVAN                         AVAILABLE  | |
| | Duration: 1 week  |  Success chance: 90%         | |
| |                                                  | |
| | Requirements: 1 Merchant, 20 Gold investment     | |
| | Rewards: +35 Gold, +5 Luxury Goods               | |
| |                                                  | |
| | [ DETAILS ] [ START MISSION ]                    | |
| +--------------------------------------------------+ |
|                                                      |
+------------------------------------------------------+
```

### Mission Detail View

```
+------------------------------------------------------+
| BANDIT CAMP RAID                          [ BACK ]   |
+------------------------------------------------------+
|                                                      |
| Duration: 3 weeks                                    |
| Success chance: 70%                                  |
|                                                      |
| Description: A group of bandits has been terrorizing |
| nearby villages. Clearing them out will improve your |
| stronghold's reputation and yield valuable loot.     |
|                                                      |
| +--------------------------------------------------+ |
| | REQUIREMENTS                                     | |
| |                                                  | |
| | NPCs:                                            | |
| | • 3 Militia (0/3 assigned)                       | |
| | • 1 Scout (0/1 assigned)                         | |
| |                                                  | |
| | Resources:                                       | |
| | • 10 Gold for supplies                           | |
| |                                                  | |
| | Buildings:                                       | |
| | • Barracks (Level 1+) - Not available            | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | REWARDS                                          | |
| |                                                  | |
| | • 50 Gold                                        | |
| | • 10 Reputation                                  | |
| | • Chance for special items                       | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
| +--------------------------------------------------+ |
| | ASSIGN NPCs                                      | |
| |                                                  | |
| | Militia: [ Select NPCs ▼ ]                       | |
| | Scout: [ Select NPCs ▼ ]                         | |
| |                                                  | |
| +--------------------------------------------------+ |
|                                                      |
| [ START MISSION ]                                    |
|                                                      |
+------------------------------------------------------+
```

## Next Turn Confirmation

```
+------------------------------------------------------+
| ADVANCE TO NEXT WEEK?                                |
+------------------------------------------------------+
|                                                      |
| You are about to advance from Week 12 to Week 13.    |
|                                                      |
| The following will occur:                            |
|                                                      |
| • Resources will be produced and consumed            |
| • Building construction will progress                |
| • Missions will advance                              |
| • Random events may occur                            |
| • Weekly report will be generated                    |
|                                                      |
| Alerts:                                              |
| ! Timber supplies are running low (-5 per week)      |
|                                                      |
| [ CANCEL ] [ CONFIRM ]                               |
|                                                      |
+------------------------------------------------------+
```

## User Interactions

### Building Management
1. **Viewing Buildings**: User clicks on Buildings tab to see all buildings
2. **Building Details**: User clicks on a building card to see detailed information
3. **New Building**: User clicks "New Building" button, selects building type, confirms placement
4. **Worker Assignment**: User clicks "Assign Workers" on a building, selects NPCs from list
5. **Building Upgrade**: User clicks "Upgrade" on a building detail view, confirms resource cost

### NPC Management
1. **Viewing NPCs**: User clicks on NPCs tab to see all NPCs
2. **NPC Details**: User clicks on an NPC card to see detailed information
3. **Recruiting**: User clicks "Recruit" button, selects NPC type, confirms cost
4. **Assignment**: User clicks "Assign" on an NPC, selects from available buildings/missions
5. **Training**: User clicks "Train" on an NPC, selects skill to improve, confirms cost

### Resource Management
1. **Viewing Resources**: User clicks on Resources tab to see all resource types
2. **Resource Details**: User clicks on a resource card to see detailed breakdown
3. **Manual Adjustment**: User clicks "Adjust" on a resource, enters amount and reason

### Journal System
1. **Viewing Journal**: User clicks on Journal tab to see chronological events
2. **Filtering**: User selects filters to narrow down journal entries
3. **Viewing Details**: User clicks on journal entry to see full details
4. **Weekly Reports**: User clicks on weekly report entries to see comprehensive summary

### Mission System
1. **Viewing Missions**: User clicks on Missions tab to see available and active missions
2. **Mission Details**: User clicks on a mission card to see detailed information
3. **Starting Mission**: User clicks "Start Mission" on a mission, assigns required NPCs and resources
4. **Checking Progress**: User views in-progress missions to check time remaining and status

### Time Progression
1. **Advancing Time**: User clicks "Next Turn" button on dashboard
2. **Confirmation**: User reviews summary of pending changes and confirms
3. **Processing**: System processes all weekly updates
4. **Results**: User is shown weekly report summarizing all changes 