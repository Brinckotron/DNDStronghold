using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DNDStrongholdApp.Models;

namespace DNDStrongholdApp.Services
{
    public static partial class CombatService
    {
        public const int MaxRaidRounds = 5;

        public static List<RaidTroop> CreateCombatants(IReadOnlyList<TroopType> troops, EnemyFaction? faction = null)
        {
            var list = new List<RaidTroop>();
            if (troops == null) return list;
            foreach (var type in troops)
            {
                var def = TroopCatalog.Get(type);
                list.Add(new RaidTroop
                {
                    Type = type,
                    Name = faction?.TroopName(type) ?? def.Name,
                    Strength = def.Strength,
                    MaxHitPoints = def.HitPoints,
                    HitPoints = def.HitPoints,
                    Stealth = def.Stealth,
                    Magic = def.Magic
                });
            }
            return list;
        }

        public static void RefreshLiveStats(RaidingParty party, EnemyFaction? faction)
        {
            if (party.Combatants == null || party.Combatants.Count == 0)
                party.Combatants = CreateCombatants(party.Troops, faction);

            var living = party.Combatants.Where(t => !t.IsDead).ToList();
            party.Troops = living.Select(t => t.Type).ToList();
            party.Stats = new RaidStatblock
            {
                Numbers = living.Count,
                Strength = living.Sum(t => t.Strength),
                HitPoints = living.Sum(t => t.HitPoints),
                MagicUsers = living.Count(t => t.Magic != MagicKind.None),
                SizeStealthPenalty = SizeStealthPenalty(living.Count)
            };
            party.Stats.Stealth = living.Sum(t => t.Stealth) + party.Stats.SizeStealthPenalty + party.StealthBonus;
            if (faction?.Outpost != null)
                party.Stats.Stealth += faction.Outpost.IntelPoints;
        }

        public static RaidBattle BeginRaid(Stronghold stronghold, RaidingParty party)
        {
            var faction = FindFaction(stronghold, party.FactionId);
            if (party.Combatants == null || party.Combatants.Count == 0)
                party.Combatants = CreateCombatants(party.Troops, faction);

            var battle = new RaidBattle
            {
                Party = party,
                StartingNumbers = Math.Max(1, party.Combatants.Count)
            };
            var stealthCast = TryOpeningDivineMagic(battle);
            RefreshLiveStats(party, faction);
            var snapshot = Calculate(stronghold);
            battle.Log.Add(BuildRaidReport(party, snapshot));
            if (!string.IsNullOrEmpty(stealthCast))
                battle.Log.Add(stealthCast);
            battle.Log.Add(string.Empty);
            return battle;
        }

        public static RaidRoundResult ResolveRound(RaidBattle battle, Stronghold stronghold, int attackerRoll, int defenderRoll)
        {
            var result = new RaidRoundResult();
            if (battle == null || battle.Ended || stronghold == null)
            {
                result.Ended = battle?.Ended ?? true;
                result.EndReason = battle?.EndReason ?? RaidEndReason.RaidersWiped;
                return result;
            }

            battle.Round++;
            result.Round = battle.Round;
            result.AttackerRoll = Math.Clamp(attackerRoll, 1, 100);
            result.DefenderRoll = Math.Clamp(defenderRoll, 1, 100);

            var faction = FindFaction(stronghold, battle.Party.FactionId);
            RefreshLiveStats(battle.Party, faction);
            var snapshot = Calculate(stronghold);
            int strength = battle.Party.Stats.Strength;
            int defense = (battle.Party.Surprise && battle.Round == 1)
                ? snapshot.RoundOneDefense
                : snapshot.TotalDefense;

            var magic = ResolveRoundMagic(battle, stronghold);

            result.AttackerTotal = result.AttackerRoll + strength;
            result.DefenderTotal = result.DefenderRoll + defense;
            int diff = result.AttackerTotal - result.DefenderTotal;
            result.Margin = Math.Abs(diff);
            result.AttackerWon = diff > 0;

            var swing = RollSwings();
            int originalBand = CasualtyBand(result.Margin, result.AttackerWon, diff == 0);
            int band = Math.Clamp(originalBand + magic.BuffSteps, 0, 10);
            int tableMargin = RepresentativeMargin(band, result.Margin, originalBand);
            CasualtiesFromMargin(tableMargin, band > 5, band == 5, swing,
                out int attackerHits, out int defenderHits);
            defenderHits += magic.ExtraDefenderHits;
            if (magic.AoeCount > 0)
                defenderHits += magic.AoeCount * LetterHits(swing, true, true, true);
            int healed = Math.Min(magic.Heals, attackerHits);
            attackerHits -= healed;
            int goalTicks = GoalTicks(battle.Party.Goal, result.Margin, result.AttackerWon);

            var lines = new List<string>
            {
                $"Round {battle.Round} — {battle.Party.Goal}."
            };
            lines.AddRange(magic.Lines);
            lines.Add($"Attackers d100 {result.AttackerRoll} + Strength {strength} = {result.AttackerTotal}.");
            lines.Add($"Defenders d100 {result.DefenderRoll} + Defense {defense} = {result.DefenderTotal}.");
            lines.Add(diff == 0
                ? "Tie. Both sides take a glancing hit."
                : result.AttackerWon
                    ? $"Attackers win by {result.Margin}."
                    : $"Defenders win by {result.Margin}.");
            if (magic.BuffSteps > 0)
            {
                lines.Add(band == originalBand
                    ? "Buff/debuff spells cannot shift the casualty table further."
                    : $"Casualty table shifts {magic.BuffSteps} step(s) toward the attackers.");
            }
            lines.Add(DescribeSwings(swing));
            if (magic.AoeCount > 0)
                lines.Add($"AoE adds {magic.AoeCount}×(a+b+c) to defender casualties.");
            if (healed > 0)
                lines.Add($"Heal spells negate {healed} raider casualty hit(s).");

            if (attackerHits > 0)
                lines.Add(ApplyAttackerDamage(battle, attackerHits));
            if (defenderHits > 0)
                lines.Add(ApplyDefenderCasualties(stronghold, battle, defenderHits));

            bool raidersAlive = battle.LivingCount > 0;
            if (goalTicks > 0 && raidersAlive)
                lines.Add(ApplyGoalTicks(battle, stronghold, goalTicks, result.Margin));
            else if (goalTicks > 0)
                lines.Add("The raiders break before they can press their goal.");

            RefreshLiveStats(battle.Party, faction);
            var after = Calculate(stronghold);
            lines.Add($"Raiders: {battle.LivingCount}/{battle.StartingNumbers} left, Strength {battle.Party.Stats.Strength}, HP {battle.Party.Stats.HitPoints}.");
            lines.Add($"Defense now {after.TotalDefense} (buildings {after.BuildingDefense} + fighters {after.NpcCombat}).");

            CheckEndOfRound(battle);
            result.Ended = battle.Ended;
            result.EndReason = battle.EndReason;
            if (battle.Ended)
                lines.Add(DescribeEnd(battle));

            result.Report = string.Join(Environment.NewLine, lines.Where(l => !string.IsNullOrWhiteSpace(l)));
            battle.RoundReports.Add(result.Report);
            battle.Log.Add(result.Report);
            battle.Log.Add(string.Empty);
            return result;
        }

        public static void FinishRaid(RaidBattle battle, Stronghold stronghold, RaidEndReason? forceReason = null)
        {
            if (battle == null || stronghold == null) return;
            if (battle.Settled) return;
            if (!battle.Ended)
            {
                battle.Ended = true;
                battle.EndReason = forceReason ?? (battle.LivingCount > 0
                    ? RaidEndReason.Escaped
                    : RaidEndReason.RaidersWiped);
            }

            bool keepSpoils = battle.LivingCount > 0
                && (battle.EndReason == RaidEndReason.Escaped
                    || battle.EndReason == RaidEndReason.ScoutFinished);

            var faction = FindFaction(stronghold, battle.Party.FactionId);
            string summary = BuildOutcomeSummary(battle, keepSpoils);
            if (keepSpoils)
                CommitSpoils(battle, stronghold, faction);
            else
                ReturnCaptures(battle, stronghold);

            battle.Log.Add(summary);
            battle.Settled = true;
        }

        public static string GoldValueText(double value) =>
            value == Math.Floor(value) ? $"{(int)value} gp" : $"{value:0.#} gp";

        public static double ResourceRaidValue(ResourceType type, int amount)
        {
            return type switch
            {
                ResourceType.Gold => amount,
                ResourceType.Food => amount * 0.5,
                ResourceType.Luxury => amount * 3,
                ResourceType.Iron => amount * 2,
                _ => 0
            };
        }

        private readonly struct SwingRolls
        {
            public int A { get; init; }
            public int B { get; init; }
            public int C { get; init; }
            public bool AHit => A <= 75;
            public bool BHit => B <= 50;
            public bool CHit => C <= 25;
        }

        private static SwingRolls RollSwings()
        {
            return new SwingRolls
            {
                A = Random.Shared.Next(1, 101),
                B = Random.Shared.Next(1, 101),
                C = Random.Shared.Next(1, 101)
            };
        }

        private static string DescribeSwings(SwingRolls swing)
        {
            return $"Swing: a {swing.A}{(swing.AHit ? " hit" : " miss")} (75%), b {swing.B}{(swing.BHit ? " hit" : " miss")} (50%), c {swing.C}{(swing.CHit ? " hit" : " miss")} (25%).";
        }

        private sealed class RoundMagic
        {
            public List<string> Lines { get; } = new();
            public int BuffSteps { get; set; }
            public int ExtraDefenderHits { get; set; }
            public int Heals { get; set; }
            public int AoeCount { get; set; }
        }

        private static readonly string[] DamageSpellNames =
        {
            "Magic Missile", "Burning Hands", "Cloud of Daggers", "Chromatic Orb"
        };

        private static readonly string[] BuffSpellNames =
        {
            "Slow", "Hypnotic Pattern", "Haste", "Fog Cloud"
        };

        private static readonly string[] AoeSpellNames =
        {
            "Lightning Bolt", "Fireball", "Ice Storm"
        };

        private static bool Chance(int percent) => Random.Shared.Next(100) < percent;

        private static string Pick(string[] names) => names[Random.Shared.Next(names.Length)];

        private static string? TryOpeningDivineMagic(RaidBattle battle)
        {
            foreach (var caster in battle.Party.Combatants.Where(t => t.IsDivine && !t.IsDead))
            {
                if (battle.StealthSpellCast)
                    break;
                if (!Chance(20))
                    continue;

                battle.StealthSpellCast = true;
                battle.Party.StealthBonus += 3;
                return $"{caster.Name} casts a Stealth spell (+3 stealth). Other Divine skip the stealth attempt.";
            }
            return null;
        }

        private static RoundMagic ResolveRoundMagic(RaidBattle battle, Stronghold stronghold)
        {
            var magic = new RoundMagic();
            var living = battle.Party.Combatants.Where(t => !t.IsDead).ToList();

            foreach (var caster in living.Where(t => t.IsDivine))
            {
                if (!Chance(15))
                    continue;
                magic.Heals++;
                magic.Lines.Add($"{caster.Name} casts Heal (negates 1 raider casualty).");
            }

            foreach (var caster in living.Where(t => t.IsArcane))
            {
                if (Chance(20))
                {
                    string spell = Pick(DamageSpellNames);
                    magic.ExtraDefenderHits++;
                    magic.Lines.Add($"{caster.Name} casts {spell} (1 damage regardless of margin).");
                    continue;
                }
                if (Chance(15))
                {
                    string spell = Pick(BuffSpellNames);
                    magic.BuffSteps++;
                    magic.Lines.Add($"{caster.Name} casts {spell} (casualty table +1 step for the attackers).");
                    continue;
                }
                if (Chance(10))
                {
                    string spell = Pick(AoeSpellNames);
                    magic.AoeCount++;
                    string buildings = DamageAoeBuildings(battle, stronghold);
                    magic.Lines.Add($"{caster.Name} casts {spell} (add a+b+c to casualties; {buildings}).");
                }
            }

            return magic;
        }

        private static string DamageAoeBuildings(RaidBattle battle, Stronghold stronghold)
        {
            int count = Random.Shared.Next(1, 3);
            var notes = new List<string>();
            var used = new HashSet<string>();
            for (int i = 0; i < count; i++)
            {
                int percent = Random.Shared.Next(10, 41);
                notes.Add(DamageRandomBuilding(battle, stronghold, percent, percent, used));
            }
            return string.Join("; ", notes);
        }

        private static int CasualtyBand(int margin, bool attackerWon, bool tie)
        {
            if (tie) return 5;
            int defenderBand = margin >= 20 ? 0 : margin >= 15 ? 1 : margin >= 10 ? 2 : margin >= 5 ? 3 : 4;
            return attackerWon ? 10 - defenderBand : defenderBand;
        }

        private static int RepresentativeMargin(int band, int originalMargin, int originalBand)
        {
            if (band == 5) return 0;
            if (band == 0 || band == 10)
                return originalBand == band ? Math.Max(20, originalMargin) : 20;
            int fromTie = Math.Abs(band - 5);
            return fromTie switch
            {
                1 => 3,
                2 => 7,
                3 => 12,
                4 => 17,
                _ => 20
            };
        }

        private static int LetterHits(SwingRolls swing, bool a, bool b, bool c)
        {
            int hits = 0;
            if (a && swing.AHit) hits++;
            if (b && swing.BHit) hits++;
            if (c && swing.CHit) hits++;
            return hits;
        }

        private static void CasualtiesFromMargin(int margin, bool attackerWon, bool tie, SwingRolls swing,
            out int attackerHits, out int defenderHits)
        {
            int loserHits;
            int winnerHits;
            if (tie)
            {
                loserHits = LetterHits(swing, a: true, b: true, c: false);
                winnerHits = loserHits;
                attackerHits = loserHits;
                defenderHits = winnerHits;
                return;
            }

            if (margin <= 4)
            {
                loserHits = 1 + LetterHits(swing, true, true, true);
                winnerHits = LetterHits(swing, true, true, false);
            }
            else if (margin <= 9)
            {
                loserHits = 2 + LetterHits(swing, true, true, true);
                winnerHits = LetterHits(swing, false, true, true);
            }
            else if (margin <= 14)
            {
                loserHits = 3 + LetterHits(swing, true, true, true);
                winnerHits = LetterHits(swing, false, true, false);
            }
            else if (margin <= 19)
            {
                loserHits = 4 + LetterHits(swing, true, true, true);
                winnerHits = LetterHits(swing, false, true, false);
            }
            else
            {
                int guaranteed = (margin + 4) / 5;
                loserHits = guaranteed + LetterHits(swing, true, true, true);
                winnerHits = LetterHits(swing, false, false, true);
            }

            if (attackerWon)
            {
                attackerHits = winnerHits;
                defenderHits = loserHits;
            }
            else
            {
                attackerHits = loserHits;
                defenderHits = winnerHits;
            }
        }

        private static int GoalTicks(OperationalGoal goal, int margin, bool attackerWon)
        {
            if (!attackerWon) return 0;
            int step = goal switch
            {
                OperationalGoal.Pillage => 5,
                OperationalGoal.Raze => 10,
                OperationalGoal.Capture => 15,
                OperationalGoal.Scout => 20,
                _ => 5
            };
            int ticks = Math.Max(1, margin / step);
            if (goal == OperationalGoal.Scout)
                ticks = Math.Min(3, ticks);
            return ticks;
        }

        private static string ApplyAttackerDamage(RaidBattle battle, int hits)
        {
            var notes = new List<string>();
            for (int i = 0; i < hits; i++)
            {
                var living = battle.Party.Combatants.Where(t => !t.IsDead).ToList();
                if (living.Count == 0) break;
                var troop = living[Random.Shared.Next(living.Count)];
                troop.HitPoints--;
                if (troop.HitPoints <= 0)
                {
                    troop.HitPoints = 0;
                    notes.Add($"{troop.Name} slain");
                }
            }

            string slain = notes.Count == 0 ? "no kills" : string.Join(", ", notes);
            return $"Raider casualties: {hits} HP ({slain}).";
        }

        private static string ApplyDefenderCasualties(Stronghold stronghold, RaidBattle battle, int hits)
        {
            var notes = new List<string>();
            var capturedIds = new HashSet<string>(battle.PendingCaptures.Select(n => n.Id));
            for (int i = 0; i < hits; i++)
            {
                var pool = CasualtyPool(stronghold, capturedIds);
                if (pool.Count == 0)
                {
                    notes.Add("no one left to hit");
                    break;
                }

                var npc = pool[Random.Shared.Next(pool.Count)];
                notes.Add(ApplyNpcCasualty(stronghold, battle, npc));
            }

            return $"Defender casualties: {string.Join("; ", notes)}.";
        }

        private static List<NPC> CasualtyPool(Stronghold stronghold, HashSet<string> capturedIds)
        {
            var present = stronghold.NPCs
                .Where(n => n.IsAlive && !capturedIds.Contains(n.Id) && !IsAway(n, stronghold))
                .ToList();
            var fighters = present.Where(n => CanFight(n, stronghold)).ToList();
            return fighters.Count > 0 ? fighters : present;
        }

        private static string ApplyNpcCasualty(Stronghold stronghold, RaidBattle battle, NPC npc)
        {
            int roll = Random.Shared.Next(100);
            string severity;
            if (npc.Hero)
            {
                if (roll < 50) severity = "light";
                else if (roll < 85) severity = "grave";
                else severity = "death";
            }
            else
            {
                if (roll < 25) severity = "light";
                else if (roll < 50) severity = "grave";
                else severity = "death";
            }

            bool wasLight = HasState(npc, NPCStateType.LightlyInjured);
            bool wasGrave = HasState(npc, NPCStateType.GravelyInjured);

            if (severity == "death" || (severity == "grave" && wasGrave))
            {
                KillNpc(stronghold, battle, npc);
                return $"{npc.Name} killed";
            }

            if (severity == "light" && wasGrave)
                return $"{npc.Name} already gravely injured";

            if (severity == "grave" || (severity == "light" && wasLight))
            {
                npc.AddHealthState(NPCStateType.GravelyInjured);
                return $"{npc.Name} gravely injured";
            }

            npc.AddHealthState(NPCStateType.LightlyInjured);
            return $"{npc.Name} lightly injured";
        }

        private static string ApplyGoalTicks(RaidBattle battle, Stronghold stronghold, int ticks, int margin)
        {
            return battle.Party.Goal switch
            {
                OperationalGoal.Pillage => ApplyPillage(battle, stronghold, ticks),
                OperationalGoal.Capture => ApplyCapture(battle, stronghold, ticks, margin),
                OperationalGoal.Scout => ApplyScout(battle, ticks),
                OperationalGoal.Raze => ApplyRaze(battle, stronghold, ticks),
                _ => string.Empty
            };
        }

        private static string ApplyPillage(RaidBattle battle, Stronghold stronghold, int ticks)
        {
            var notes = new List<string>();
            for (int i = 0; i < ticks; i++)
            {
                string stolen = StealOneBundle(battle, stronghold);
                if (!string.IsNullOrEmpty(stolen))
                    notes.Add(stolen);
            }

            // A bit of building damage: one hit if they scored any pillage ticks this clash.
            notes.Add(DamageRandomBuilding(battle, stronghold));
            string loot = notes.Count == 0 ? "nothing left to take" : string.Join("; ", notes);
            return $"Pillage: {loot}. Bags now {GoldValueText(battle.LootGoldValue)} (flee at {GoldValueText(50.0 * battle.Party.Power)}).";
        }

        private static string StealOneBundle(RaidBattle battle, Stronghold stronghold)
        {
            var options = new List<(ResourceType type, int weight, int minTake, int maxTake)>
            {
                (ResourceType.Luxury, 40, 1, 2),
                (ResourceType.Gold, 40, 8, 15),
                (ResourceType.Food, 10, 4, 8),
                (ResourceType.Iron, 10, 2, 4)
            };

            var available = options
                .Select(o => (o.type, o.weight, o.minTake, o.maxTake, amount: GetResourceAmount(stronghold, battle, o.type)))
                .Where(o => o.amount > 0)
                .ToList();
            if (available.Count == 0)
                return string.Empty;

            int totalWeight = available.Sum(o => o.weight);
            int pick = Random.Shared.Next(totalWeight);
            int cursor = 0;
            var chosen = available[^1];
            foreach (var option in available)
            {
                cursor += option.weight;
                if (pick < cursor)
                {
                    chosen = option;
                    break;
                }
            }

            int want = Random.Shared.Next(chosen.minTake, chosen.maxTake + 1);
            int take = Math.Min(want, chosen.amount);
            if (take <= 0) return string.Empty;

            AddPendingLoot(battle, chosen.type, take);
            return $"{take} {chosen.type}";
        }

        private static void AddPendingLoot(RaidBattle battle, ResourceType type, int amount)
        {
            if (!battle.PendingLoot.ContainsKey(type))
                battle.PendingLoot[type] = 0;
            battle.PendingLoot[type] += amount;
            battle.LootGoldValue += ResourceRaidValue(type, amount);
        }

        private const int MaxCapturedNpcs = 3;
        private const int CaptureNpcMinMargin = 5;

        private static string ApplyCapture(RaidBattle battle, Stronghold stronghold, int ticks, int margin)
        {
            var notes = new List<string>();
            for (int i = 0; i < ticks; i++)
            {
                int step = battle.CaptureGoalTicks;
                battle.CaptureGoalTicks++;
                string spoils = StealCaptureSpoils(battle, stronghold, step);
                if (!string.IsNullOrEmpty(spoils))
                    notes.Add(spoils);
            }

            if (battle.PendingCaptures.Count < MaxCapturedNpcs)
            {
                if (margin < CaptureNpcMinMargin)
                {
                    notes.Add("too close to snatch anyone");
                }
                else
                {
                    var target = PickCaptureTarget(battle, stronghold);
                    if (target == null)
                    {
                        notes.Add("no one left to seize");
                    }
                    else
                    {
                        UnassignNpc(stronghold, target);
                        stronghold.NPCs.Remove(target);
                        battle.PendingCaptures.Add(target);
                        notes.Add($"seized {target.Name}");
                    }
                }
            }

            string bags = battle.LootGoldValue > 0 ? $" Bags {GoldValueText(battle.LootGoldValue)}." : "";
            return $"Capture: {string.Join(", ", notes)}. Held {battle.PendingCaptures.Count}/{MaxCapturedNpcs}.{bags}";
        }

        private static string StealCaptureSpoils(RaidBattle battle, Stronghold stronghold, int stepIndex)
        {
            int targetValue = Random.Shared.Next(2, 6);
            if (stepIndex >= 3)
                targetValue *= 2;

            var taken = new List<string>();
            double need = targetValue;

            int gold = DrainCaptureResource(battle, stronghold, ResourceType.Gold, (int)Math.Floor(need));
            if (gold > 0)
            {
                taken.Add($"{gold} Gold");
                need -= gold;
            }

            int luxury = DrainCaptureByValue(battle, stronghold, ResourceType.Luxury, 3.0, ref need);
            if (luxury > 0)
                taken.Add($"{luxury} Luxury");

            int iron = DrainCaptureByValue(battle, stronghold, ResourceType.Iron, 2.0, ref need);
            if (iron > 0)
                taken.Add($"{iron} Iron");

            int food = DrainCaptureByValue(battle, stronghold, ResourceType.Food, 0.5, ref need);
            if (food > 0)
                taken.Add($"{food} Food");

            if (need > 0)
            {
                if (GetResourceAmount(stronghold, battle, ResourceType.Luxury) > 0)
                {
                    DrainCaptureResource(battle, stronghold, ResourceType.Luxury, 1);
                    taken.Add("1 Luxury");
                    need = 0;
                }
                else if (GetResourceAmount(stronghold, battle, ResourceType.Iron) > 0)
                {
                    DrainCaptureResource(battle, stronghold, ResourceType.Iron, 1);
                    taken.Add("1 Iron");
                    need = 0;
                }
            }

            return taken.Count == 0 ? string.Empty : string.Join(" + ", taken);
        }

        private static int DrainCaptureByValue(
            RaidBattle battle, Stronghold stronghold, ResourceType type, double unitValue, ref double need)
        {
            int taken = 0;
            while (need >= unitValue - 0.0001)
            {
                if (GetResourceAmount(stronghold, battle, type) <= 0)
                    break;
                DrainCaptureResource(battle, stronghold, type, 1);
                taken++;
                need -= unitValue;
            }
            return taken;
        }

        private static int DrainCaptureResource(
            RaidBattle battle, Stronghold stronghold, ResourceType type, int amount)
        {
            int have = GetResourceAmount(stronghold, battle, type);
            int take = Math.Min(have, amount);
            if (take > 0)
                AddPendingLoot(battle, type, take);
            return take;
        }

        private static NPC? PickCaptureTarget(RaidBattle battle, Stronghold stronghold)
        {
            var held = new HashSet<string>(battle.PendingCaptures.Select(n => n.Id));
            var present = stronghold.NPCs
                .Where(n => n.IsAlive && !held.Contains(n.Id) && !IsAway(n, stronghold))
                .ToList();
            if (present.Count == 0) return null;

            var tier1 = present.Where(n =>
                GetSkillLevel(n) <= 0
                && !HasState(n, NPCStateType.Sick)
                && !HasState(n, NPCStateType.LightlyInjured)
                && !HasState(n, NPCStateType.GravelyInjured)).ToList();
            if (tier1.Count > 0)
                return tier1[Random.Shared.Next(tier1.Count)];

            var tier2 = present.Where(n =>
                GetSkillLevel(n) > 0 || HasState(n, NPCStateType.LightlyInjured)).ToList();
            if (tier2.Count > 0)
                return tier2[Random.Shared.Next(tier2.Count)];

            return present[Random.Shared.Next(present.Count)];
        }

        private static string ApplyScout(RaidBattle battle, int ticks)
        {
            battle.PendingIntel += ticks;
            return $"Scout: +{ticks} intel (now {battle.PendingIntel}).";
        }

        private static string ApplyRaze(RaidBattle battle, Stronghold stronghold, int ticks)
        {
            var notes = new List<string>();
            for (int i = 0; i < ticks; i++)
                notes.Add(DamageRandomBuilding(battle, stronghold));
            return $"Raze: {string.Join("; ", notes)}.";
        }

        private static string DamageRandomBuilding(RaidBattle battle, Stronghold stronghold)
        {
            return DamageRandomBuilding(battle, stronghold, 10, 20, null);
        }

        private static string DamageRandomBuilding(
            RaidBattle battle, Stronghold stronghold, int minPercent, int maxPercent, HashSet<string>? used)
        {
            var buildings = stronghold.Buildings
                .Where(b => b.ConstructionStatus != BuildingStatus.Planning && b.Condition > 0)
                .Where(b => used == null || !used.Contains(b.Id))
                .ToList();
            if (buildings.Count == 0)
            {
                buildings = stronghold.Buildings
                    .Where(b => b.ConstructionStatus != BuildingStatus.Planning && b.Condition > 0)
                    .ToList();
            }
            if (buildings.Count == 0)
                return "no standing building to damage";

            var building = buildings[Random.Shared.Next(buildings.Count)];
            used?.Add(building.Id);
            int percent = minPercent >= maxPercent
                ? minPercent
                : Random.Shared.Next(minPercent, maxPercent + 1);
            var cancelled = building.Damage(percent);
            if (cancelled != null)
                battle.CancelledProjects.Add(cancelled);
            return $"{building.Name} {percent}% damage (now {building.Condition}%)";
        }

        private static void CheckEndOfRound(RaidBattle battle)
        {
            if (battle.LivingCount <= 0)
            {
                battle.Ended = true;
                battle.EndReason = RaidEndReason.RaidersWiped;
                return;
            }

            if (ShouldFlee(battle))
            {
                battle.Ended = true;
                battle.EndReason = battle.Party.Goal == OperationalGoal.Scout
                    ? RaidEndReason.ScoutFinished
                    : RaidEndReason.Escaped;
                return;
            }

            int cap = battle.Party.Goal == OperationalGoal.Raze ? 10 : MaxRaidRounds;
            if (battle.Round >= cap)
            {
                battle.Ended = true;
                battle.EndReason = RaidEndReason.Escaped;
            }
        }

        private static bool ShouldFlee(RaidBattle battle)
        {
            int living = battle.LivingCount;
            int start = Math.Max(1, battle.StartingNumbers);
            return battle.Party.Goal switch
            {
                OperationalGoal.Pillage =>
                    battle.LootGoldValue >= 50.0 * battle.Party.Power
                    || living * 2 <= start,
                OperationalGoal.Capture =>
                    battle.PendingCaptures.Count >= MaxCapturedNpcs
                    || living * 3 <= start,
                OperationalGoal.Scout => battle.Round >= 2,
                OperationalGoal.Raze => false,
                _ => false
            };
        }

        private static void CommitSpoils(RaidBattle battle, Stronghold stronghold, EnemyFaction? faction)
        {
            foreach (var pair in battle.PendingLoot)
            {
                var resource = stronghold.Resources.Find(r => r.Type == pair.Key);
                if (resource == null) continue;
                resource.Amount = Math.Max(0, resource.Amount - pair.Value);
                if (pair.Key == ResourceType.Gold)
                    stronghold.Treasury = resource.Amount;
            }

            if (faction != null)
            {
                faction.CapturedNpcs ??= new List<NPC>();
                foreach (var npc in battle.PendingCaptures)
                    faction.CapturedNpcs.Add(npc);

                if (battle.PendingIntel > 0)
                    GrantIntel(faction, battle.PendingIntel);
            }

            battle.PendingCaptures.Clear();
        }

        private static void GrantIntel(EnemyFaction faction, int intel)
        {
            if (intel <= 0) return;
            if (!faction.HasOutpost)
            {
                faction.Outpost = new FactionOutpost
                {
                    Name = $"{faction.Name} Outpost",
                    Notes = "Raised from raid intel."
                };
                intel--;
            }

            if (intel > 0)
                faction.Outpost!.IntelPoints += intel;
        }

        private static void ReturnCaptures(RaidBattle battle, Stronghold stronghold)
        {
            foreach (var npc in battle.PendingCaptures)
            {
                if (!stronghold.NPCs.Any(n => n.Id == npc.Id))
                    stronghold.NPCs.Add(npc);
            }
            battle.PendingCaptures.Clear();
        }

        private static string BuildOutcomeSummary(RaidBattle battle, bool keepSpoils)
        {
            var sb = new StringBuilder();
            if (!keepSpoils)
            {
                if (battle.PendingCaptures.Count > 0 || battle.PendingLoot.Count > 0 || battle.PendingIntel > 0)
                    sb.AppendLine("The raiders did not get away. Captives are freed and bags and intel are lost.");
                return sb.ToString().TrimEnd();
            }

            if (battle.PendingLoot.Count > 0 || battle.LootGoldValue > 0)
                sb.AppendLine($"Stolen: {FormatPendingLoot(battle)} ({GoldValueText(battle.LootGoldValue)}).");
            if (battle.PendingCaptures.Count > 0)
                sb.AppendLine($"Captured: {string.Join(", ", battle.PendingCaptures.Select(n => n.Name))}.");
            if (battle.PendingIntel > 0)
                sb.AppendLine($"Intel secured: {battle.PendingIntel}.");
            return sb.ToString().TrimEnd();
        }

        private static string FormatPendingLoot(RaidBattle battle)
        {
            if (battle.PendingLoot.Count == 0) return "nothing";
            return string.Join(", ", battle.PendingLoot.Select(p => $"{p.Value} {p.Key}"));
        }

        public static string DescribeEnd(RaidBattle battle)
        {
            return battle.EndReason switch
            {
                RaidEndReason.RaidersWiped => "The raiding party is destroyed.",
                RaidEndReason.ScoutFinished => "The scouts withdraw with whatever intel they gathered.",
                RaidEndReason.Escaped => "The raiders break off and escape.",
                _ => "The raid ends."
            };
        }

        private static void KillNpc(Stronghold stronghold, RaidBattle battle, NPC npc)
        {
            UnassignNpc(stronghold, npc);
            npc.IsAlive = false;
            npc.States.Clear();
            stronghold.NPCs.Remove(npc);
            battle.KilledNpcs.Add(npc.Name);
        }

        public static void UnassignNpc(Stronghold stronghold, NPC npc)
        {
            npc.Assignment = new NPCAssignment();
            npc.UpdateStatus();
            foreach (var building in stronghold.Buildings)
            {
                building.AssignedWorkers?.Remove(npc.Id);
                building.DedicatedConstructionCrew?.Remove(npc.Id);
                building.CurrentProject?.AssignedWorkers?.Remove(npc.Id);
            }
        }

        private static int GetResourceAmount(Stronghold stronghold, RaidBattle battle, ResourceType type)
        {
            int have = stronghold.Resources.Find(r => r.Type == type)?.Amount ?? 0;
            battle.PendingLoot.TryGetValue(type, out int pending);
            return Math.Max(0, have - pending);
        }

        private static int GetSkillLevel(NPC npc) =>
            ProjectResolutionService.GetSkillLevel(npc, "Combat");

        private static EnemyFaction? FindFaction(Stronghold stronghold, string factionId)
        {
            return stronghold.EnemyFactions?.Find(f => f.Id == factionId);
        }
    }
}
