using System;
using System.Collections.Generic;
using System.Linq;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    // Single source of truth for the spellcaster rules. The healing dialog, the
    // Magic Assist dialog, the raid dialog and the weekly tick all read from here so
    // they cannot disagree about eligibility, costs, or point pools.
    public static class MagicService
    {
        public const int PointsPerIncrement = 5;
        public const int MagicAssistGoldPerIncrement = 10;

        public const int LightInjuryHealPoints = 1;
        public const int GraveInjuryHealPoints = 3;
        public const int SicknessHealPoints = 3;
        public const int GraveInjuryHealGold = 10;
        public const int SicknessHealGold = 10;

        // A spellcaster is only usable if the DM flagged them, they are alive, and they
        // are not off on a Trade Mission / Reconnaissance style project.
        public static bool IsEligibleCaster(NPC npc, Stronghold stronghold)
        {
            if (npc == null || !npc.Spellcaster || !npc.IsAlive) return false;
            return !CombatService.IsAway(npc, stronghold);
        }

        public static bool IsEligibleHealer(NPC npc, Stronghold stronghold)
        {
            return IsEligibleCaster(npc, stronghold) && npc.FaithLevel >= 1;
        }

        public static bool IsEligibleArcanist(NPC npc, Stronghold stronghold)
        {
            return IsEligibleCaster(npc, stronghold) && npc.ArcanaLevel >= 1;
        }

        public static List<NPC> AvailableHealers(Stronghold stronghold)
        {
            if (stronghold?.NPCs == null) return new List<NPC>();
            return stronghold.NPCs
                .Where(n => IsEligibleHealer(n, stronghold) && n.HealPointsRemaining > 0)
                .ToList();
        }

        public static List<NPC> AvailableArcanists(Stronghold stronghold)
        {
            if (stronghold?.NPCs == null) return new List<NPC>();
            return stronghold.NPCs
                .Where(n => IsEligibleArcanist(n, stronghold) && n.SpellPointsRemaining > 0)
                .ToList();
        }

        public static (int Points, int Gold) HealCost(NPCStateType state)
        {
            return state switch
            {
                NPCStateType.LightlyInjured => (LightInjuryHealPoints, 0),
                NPCStateType.GravelyInjured => (GraveInjuryHealPoints, GraveInjuryHealGold),
                NPCStateType.Sick => (SicknessHealPoints, SicknessHealGold),
                _ => (0, 0)
            };
        }

        public static string DescribeState(NPCStateType state)
        {
            return state switch
            {
                NPCStateType.LightlyInjured => "Lightly Injured",
                NPCStateType.GravelyInjured => "Gravely Injured",
                NPCStateType.Sick => "Sick",
                _ => state.ToString()
            };
        }

        // Conditions a healer can treat. Sickness is a disease, not a wound, so it is
        // off the table in the middle of a fight.
        public static IEnumerable<NPCStateType> TreatableStates(NPC patient, bool raidMode)
        {
            if (patient?.States == null) yield break;

            foreach (var state in patient.States
                .Select(s => s.Type)
                .Distinct()
                .OrderByDescending(s => s == NPCStateType.GravelyInjured))
            {
                if (raidMode && state == NPCStateType.Sick) continue;
                yield return state;
            }
        }

        public static bool CanAffordHeal(NPC healer, Stronghold stronghold, NPCStateType state)
        {
            var (points, gold) = HealCost(state);
            if (points <= 0) return false;
            if (healer.HealPointsRemaining < points) return false;
            return gold <= 0 || GetGold(stronghold) >= gold;
        }

        // Spends heal points and gold, then clears the condition. A gravely wounded
        // patient is fully healed, so any lighter wound goes with it.
        public static bool TryHeal(NPC healer, NPC patient, Stronghold stronghold, NPCStateType state,
            bool raidMode, out string message)
        {
            message = string.Empty;
            if (healer == null || patient == null || stronghold == null) return false;

            if (!IsEligibleHealer(healer, stronghold))
            {
                message = $"{healer?.Name} cannot cast right now.";
                return false;
            }

            if (raidMode && state == NPCStateType.Sick)
            {
                message = "Sickness cannot be cured in the middle of a raid.";
                return false;
            }

            if (!patient.States.Any(s => s.Type == state))
            {
                message = $"{patient.Name} is no longer {DescribeState(state)}.";
                return false;
            }

            var (points, gold) = HealCost(state);
            if (points <= 0)
            {
                message = "That condition cannot be healed with magic.";
                return false;
            }

            if (healer.HealPointsRemaining < points)
            {
                message = $"{healer.Name} has {healer.HealPointsRemaining} heal point(s) left; that needs {points}.";
                return false;
            }

            if (gold > 0 && !SpendGold(stronghold, gold))
            {
                message = $"Not enough gold ({gold} needed).";
                return false;
            }

            healer.HealPointsUsed += points;

            if (state == NPCStateType.GravelyInjured)
            {
                patient.RemoveHealthState(NPCStateType.GravelyInjured);
                patient.RemoveHealthState(NPCStateType.LightlyInjured);
            }
            else
            {
                patient.RemoveHealthState(state);
            }

            string cost = gold > 0 ? $"{points} heal point(s) and {gold} gold" : $"{points} heal point(s)";
            message = $"{healer.Name} healed {patient.Name} of {DescribeState(state)} for {cost}.";
            return true;
        }

        public static int GetGold(Stronghold stronghold)
        {
            return stronghold?.Resources?.Find(r => r.Type == ResourceType.Gold)?.Amount ?? 0;
        }

        // Mirrors Building.StartRepair: adjust the resource then keep Treasury in sync.
        public static bool SpendGold(Stronghold stronghold, int amount)
        {
            if (amount <= 0) return true;
            if (stronghold == null) return false;

            var gold = stronghold.Resources?.Find(r => r.Type == ResourceType.Gold);
            if (gold == null || gold.Amount < amount) return false;

            gold.Amount -= amount;
            stronghold.Treasury = gold.Amount;
            return true;
        }

        // ---- Magic Assist ----

        public static bool BuildingAcceptsMagicAssist(Building building)
        {
            return building != null
                && (building.ConstructionStatus == BuildingStatus.Planning
                    || building.ConstructionStatus == BuildingStatus.UnderConstruction
                    || building.ConstructionStatus == BuildingStatus.Repairing
                    || building.ConstructionStatus == BuildingStatus.Upgrading);
        }

        public static bool IsAssignedToBuilding(NPC npc, Building building)
        {
            if (npc == null || building == null) return false;
            return (building.AssignedWorkers != null && building.AssignedWorkers.Contains(npc.Id))
                || (building.DedicatedConstructionCrew != null && building.DedicatedConstructionCrew.Contains(npc.Id));
        }

        // Casters who can boost this building: tagged for Magic Assist, assigned here,
        // with a spellcasting level.
        public static List<NPC> AssistCasters(Building building, Stronghold stronghold)
        {
            if (building == null || stronghold?.NPCs == null) return new List<NPC>();
            return stronghold.NPCs
                .Where(n => IsAssignedToBuilding(n, building) && CanAssistHere(n, building, stronghold))
                .ToList();
        }

        private static bool CanAssistHere(NPC npc, Building building, Stronghold stronghold)
        {
            return npc.CanMagicAssist && IsEligibleCaster(npc, stronghold);
        }

        // Increments available this week: combined spellcasting levels, minus anything
        // already pledged on this building.
        public static int MaxAssistIncrements(Building building, Stronghold stronghold)
        {
            int capacity = AssistCasters(building, stronghold).Sum(n => n.SpellcastingLevel);
            return Math.Max(0, capacity - PledgedIncrements(building));
        }

        public static int PledgedIncrements(Building building)
        {
            if (building?.PendingMagicAssist == null) return 0;
            return building.PendingMagicAssist.Sum(p => p.Increments);
        }

        public static int PledgedPoints(Building building)
        {
            return PledgedIncrements(building) * PointsPerIncrement;
        }

        // Same formula UpdateConstructionProgress uses, including Planning buildings
        // that have not started the weekly tick yet.
        public static int WorkerConstructionPoints(Building building, Stronghold stronghold)
        {
            if (building == null || stronghold?.NPCs == null) return 0;

            int points = 0;
            AddCrew(building.AssignedWorkers);
            AddCrew(building.DedicatedConstructionCrew);
            return points;

            void AddCrew(List<string> ids)
            {
                if (ids == null) return;
                foreach (var id in ids)
                {
                    var worker = stronghold.NPCs.Find(n => n.Id == id);
                    if (worker == null) continue;
                    points += TraitService.ModifyConstructionPoints(worker, 1 + worker.GetSkillLevel("Construction"));
                }
            }
        }

        public static bool CanOfferMagicAssist(Building building, Stronghold stronghold)
        {
            return BuildingAcceptsMagicAssist(building) && AssistCasters(building, stronghold).Count > 0;
        }

        // Validates pledges against the current roster and returns the points to add to
        // this week's construction. A caster pulled off the building voids their pledge;
        // the gold is not refunded.
        public static int ConsumeMagicAssist(Building building, Stronghold stronghold, out List<string> voided)
        {
            voided = new List<string>();
            if (building?.PendingMagicAssist == null || building.PendingMagicAssist.Count == 0)
                return 0;

            int points = 0;
            foreach (var pledge in building.PendingMagicAssist)
            {
                var npc = stronghold?.NPCs?.Find(n => n.Id == pledge.NpcId);
                if (npc != null && npc.IsAlive && npc.CanMagicAssist && IsAssignedToBuilding(npc, building))
                {
                    points += pledge.Increments * PointsPerIncrement;
                }
                else
                {
                    voided.Add(pledge.NpcName);
                }
            }

            building.PendingMagicAssist.Clear();
            return points;
        }
    }
}
