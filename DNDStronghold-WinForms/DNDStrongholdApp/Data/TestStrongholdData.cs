using System;
using System.Collections.Generic;
using System.Linq;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    public static class TestStrongholdData
    {
        public static Stronghold GetTestStronghold()
        {
            var stronghold = new Stronghold
            {
                Name = "Default Stronghold",
                Location = "Somewhere",
                Buildings = new List<Building>(),
                NPCs = new List<NPC>(),
                Resources = new List<Resource>(),
                Journal = new List<JournalEntry>()
            };

            // 1. Create Buildings (cycle through states, set levels)
            var buildingTypes = Enum.GetValues(typeof(BuildingType)).Cast<BuildingType>().ToList();
            var statuses = Enum.GetValues(typeof(BuildingStatus)).Cast<BuildingStatus>().ToList();
            int statusIdx = 0;
            foreach (var type in buildingTypes)
            {
                var status = statuses[statusIdx % statuses.Count];
                var building = new Building(type)
                {
                    Name = $"{type} ({status})",
                    ConstructionStatus = status,
                    Condition = status == BuildingStatus.Damaged ? 40 : 100,
                    Level = 1 + (statusIdx % 3)
                };
                stronghold.Buildings.Add(building);
                statusIdx++;
            }

            // 2. Create 4 NPCs of each type: 3 healthy (Available), 1 sick/injured (Unavailable)
            var npcTypes = Enum.GetValues(typeof(NPCType)).Cast<NPCType>().ToList();
            var healthStates = Enum.GetValues(typeof(NPCStateType)).Cast<NPCStateType>().ToList();
            int npcIdx = 0;
            for (int t = 0; t < npcTypes.Count; t++)
            {
                var type = npcTypes[t];
                // 3 healthy
                for (int i = 0; i < 3; i++)
                {
                    var npc = new NPC(type, $"{type} {npcIdx * 4 + i + 1}")
                    {
                        Status = NPCStatus.Available,
                        Level = 1 + ((npcIdx + i) % 3)
                    };
                    if ((npcIdx + i) % 2 == 0)
                    {
                        var skill = npc.Skills.FirstOrDefault();
                        if (skill != null) skill.Level = 2 + ((npcIdx + i) % 3);
                    }
                    stronghold.NPCs.Add(npc);
                }
                // 1 sick/injured
                var sickNpc = new NPC(type, $"{type} {npcIdx * 4 + 4}")
                {
                    Status = NPCStatus.Unavailable,
                    Level = 1 + ((npcIdx + 3) % 3)
                };
                sickNpc.States.Add(new NPCState { Type = healthStates[npcIdx % healthStates.Count] });
                stronghold.NPCs.Add(sickNpc);
                npcIdx++;
            }

            // 3. Assign healthy NPCs to buildings using a queue
            var availableWorkersQueue = new Queue<NPC>(stronghold.NPCs.Where(n => n.Status == NPCStatus.Available && n.Assignment.Type == AssignmentType.Unassigned && (n.States == null || n.States.Count == 0)));
            int mustLeaveUnassigned = 2;
            var mustAssignStatuses = new[] { BuildingStatus.Planning, BuildingStatus.UnderConstruction, BuildingStatus.Upgrading, BuildingStatus.Repairing };
            var canAssignStatuses = new[] { BuildingStatus.Complete, BuildingStatus.Damaged };
            var mustAssignBuildings = stronghold.Buildings.Where(b => mustAssignStatuses.Contains(b.ConstructionStatus)).ToList();
            var canAssignBuildings = stronghold.Buildings.Where(b => canAssignStatuses.Contains(b.ConstructionStatus)).ToList();

            // Assign one available worker to each must-assign building
            foreach (var building in mustAssignBuildings)
            {
                if (availableWorkersQueue.Count > mustLeaveUnassigned)
                {
                    var worker = availableWorkersQueue.Dequeue();
                    building.AssignedWorkers.Add(worker.Id);
                    worker.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Building,
                        TargetId = building.Id,
                        TargetName = building.Name
                    };
                    worker.Status = NPCStatus.BuildingAssigned;
                }
            }

            // Assign remaining available workers to other buildings, except 2
            foreach (var building in canAssignBuildings)
            {
                while (availableWorkersQueue.Count > mustLeaveUnassigned)
                {
                    var worker = availableWorkersQueue.Dequeue();
                    building.AssignedWorkers.Add(worker.Id);
                    worker.Assignment = new NPCAssignment
                    {
                        Type = AssignmentType.Building,
                        TargetId = building.Id,
                        TargetName = building.Name
                    };
                    worker.Status = NPCStatus.BuildingAssigned;
                }
            }

            // 4. Add resources
            stronghold.Resources.Add(new Resource { Type = ResourceType.Gold, Amount = 1000 });
            stronghold.Resources.Add(new Resource { Type = ResourceType.Food, Amount = 200 });
            stronghold.Resources.Add(new Resource { Type = ResourceType.Stone, Amount = 500 });
            stronghold.Resources.Add(new Resource { Type = ResourceType.Wood, Amount = 300 });
            stronghold.Resources.Add(new Resource { Type = ResourceType.Iron, Amount = 50 });
            stronghold.Resources.Add(new Resource { Type = ResourceType.Luxury, Amount = 50 });

            // 5. Refresh construction progress for all buildings
            foreach (var building in stronghold.Buildings)
            {
                building.UpdateConstructionProgress(stronghold.NPCs);
            }

            return stronghold;
        }
    }
} 