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

        public static List<NPC> AvailableHeroes(Stronghold stronghold, RaidBattle battle)
        {
            if (stronghold?.NPCs == null || battle == null) return new List<NPC>();
            var used = battle.HeroicActsUsed ?? new List<string>();
            return stronghold.NPCs
                .Where(n => n.Hero
                    && n.IsAlive
                    && CanFight(n, stronghold)
                    && !used.Contains(n.Id))
                .ToList();
        }

        public static void SpendHeroicAct(RaidBattle battle, NPC hero)
        {
            if (battle == null || hero == null) return;
            battle.HeroicActsUsed ??= new List<string>();
            if (!battle.HeroicActsUsed.Contains(hero.Id))
                battle.HeroicActsUsed.Add(hero.Id);
        }

        // 1 HP on a chosen raider, applied before the round is calculated so a kill
        // drops that troop's Strength from this comparison.
        public static string ApplyStrikeTrue(RaidBattle battle, Stronghold stronghold, NPC hero, RaidTroop troop)
        {
            if (battle == null || troop == null || troop.IsDead)
                return string.Empty;

            troop.HitPoints = Math.Max(0, troop.HitPoints - 1);
            SpendHeroicAct(battle, hero);
            var faction = FindFaction(stronghold, battle.Party.FactionId);
            RefreshLiveStats(battle.Party, faction);

            string fate = troop.IsDead ? "slain" : $"{troop.HitPoints} HP left";
            return $"{hero.Name} Strikes True at {troop.Name} ({fate}).";
        }

        public static RaidRoundResult ResolveRound(RaidBattle battle, Stronghold stronghold, int attackerRoll, int defenderRoll)
        {
            var preview = StartRoundResolution(battle, stronghold, attackerRoll, defenderRoll);
            preview.ProjectCasualties();
            return preview.Commit();
        }

        public static RaidRoundPreview StartRoundResolution(RaidBattle battle, Stronghold stronghold, int attackerRoll, int defenderRoll)
        {
            return new RaidRoundPreview(battle, stronghold, attackerRoll, defenderRoll);
        }

        // Holds a round after magic and the d100 comparison, but before swing dice and
        // casualties. Rally mutates the defender roll here. Hold the Line remaps defender
        // casualty dice and must be declared before ProjectCasualties. Interpose edits
        // the projected list after.
        public sealed class RaidRoundPreview
        {
            private readonly RaidBattle _battle;
            private readonly Stronghold _stronghold;
            private readonly EnemyFaction? _faction;
            private readonly RoundMagic _magic;
            private readonly DefenderMagic _defenderMagic;
            private readonly List<string> _heroicLines = new();
            private readonly RaidRoundResult _result = new();
            private SwingContext? _swing;
            private int _strength;
            private int _defense;
            private int _attackerHits;
            private int _defenderHits;
            private int _healed;
            private int _goalTicks;
            private int _originalBand;
            private int _band;
            private bool _projected;
            private bool _holdTheLine;

            public RaidRoundPreview(RaidBattle battle, Stronghold stronghold, int attackerRoll, int defenderRoll)
            {
                _battle = battle;
                _stronghold = stronghold;

                if (battle == null || battle.Ended || stronghold == null)
                {
                    _result.Ended = battle?.Ended ?? true;
                    _result.EndReason = battle?.EndReason ?? RaidEndReason.RaidersWiped;
                    _magic = new RoundMagic();
                    _defenderMagic = new DefenderMagic();
                    return;
                }

                battle.Round++;
                _result.Round = battle.Round;
                _result.AttackerRoll = Math.Clamp(attackerRoll, 1, 100);
                _result.DefenderRoll = Math.Clamp(defenderRoll, 1, 100);

                _faction = FindFaction(stronghold, battle.Party.FactionId);
                RefreshLiveStats(battle.Party, _faction);
                var snapshot = Calculate(stronghold);
                _strength = battle.Party.Stats.Strength;
                _defense = (battle.Party.Surprise && battle.Round == 1)
                    ? snapshot.RoundOneDefense
                    : snapshot.TotalDefense;

                _magic = ResolveRoundMagic(battle, stronghold);
                _defenderMagic = ResolveDefenderMagic(battle);
                RecalculateTotals();
            }

            public RaidRoundResult Result => _result;
            public bool Ready => _battle != null && !_battle.Ended && _stronghold != null;
            public bool AttackerWon => _result.AttackerWon;
            public bool Tied => _result.AttackerTotal == _result.DefenderTotal;
            public int Strength => _strength;
            public int Defense => _defense;
            public int GoalTicks => _goalTicks;
            public bool HoldTheLineActive => _holdTheLine;
            public List<ProjectedCasualty> DefenderCasualties { get; } = new();

            public string ComparisonLine()
            {
                if (Tied) return "Tie. Both sides take a glancing hit.";
                return AttackerWon
                    ? $"Attackers win by {_result.Margin}."
                    : $"Defenders win by {_result.Margin}.";
            }

            public void AddHeroicLine(string line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    _heroicLines.Add(line);
            }

            public void ApplyRally(int newDefenderRoll)
            {
                if (!Ready || _projected) return;
                _result.DefenderRoll = Math.Clamp(newDefenderRoll, 1, 100);
                RecalculateTotals();
            }

            public void ProjectCasualties()
            {
                if (!Ready || _projected) return;

                _swing = new SwingContext();
                bool tie = Tied;
                _originalBand = CasualtyBand(_result.Margin, _result.AttackerWon, tie);
                _band = Math.Clamp(_originalBand + _magic.BuffSteps, 0, 10);
                int tableMargin = RepresentativeMargin(_band, _result.Margin, _originalBand);
                CasualtiesFromMargin(tableMargin, _band > 5, _band == 5, _swing, _defenderMagic.BarrierActive,
                    _holdTheLine, out _attackerHits, out _defenderHits);
                _defenderHits += _magic.ExtraDefenderHits;
                for (int i = 0; i < _magic.AoeCount; i++)
                    _defenderHits += ResolveAoeAgainstDefenders(_swing, _defenderMagic.BarrierActive, _holdTheLine);
                _attackerHits += _defenderMagic.ExtraAttackerHits;
                for (int i = 0; i < _defenderMagic.FireballCount; i++)
                    _attackerHits += 1 + _swing.AA() + _swing.BB() + _swing.CC();
                _healed = Math.Min(_magic.Heals, _attackerHits);
                _attackerHits -= _healed;
                _goalTicks = _holdTheLine
                    ? 0
                    : GoalTicks(_battle.Party.Goal, _result.Margin, _result.AttackerWon);

                DefenderCasualties.Clear();
                DefenderCasualties.AddRange(ProjectDefenderCasualties(_stronghold, _battle, _defenderHits));
                _projected = true;
            }

            public bool Interpose(int index, NPC hero)
            {
                if (!_projected || hero == null) return false;
                if (index < 0 || index >= DefenderCasualties.Count) return false;

                var hit = DefenderCasualties[index];
                if (!hit.IsMeaningful || hit.Npc.Id == hero.Id) return false;

                string saved = hit.Npc.Name;
                RetargetCasualty(hit, hero, DefenderCasualties.Take(index));
                AddHeroicLine($"{hero.Name} Interposes, taking the blow meant for {saved} ({hit.Note}).");
                return true;
            }

            public void HoldTheLine(NPC hero)
            {
                if (!Ready || _projected || hero == null) return;
                _holdTheLine = true;
                AddHeroicLine($"{hero.Name} Holds the Line. Defender dice step down (a→b, b→c, c ignored; aa→bb, bb→cc, cc ignored), and the raiders gain no ground.");
            }

            public RaidRoundResult Commit()
            {
                if (!Ready)
                    return _result;
                if (!_projected)
                    ProjectCasualties();

                int diff = _result.AttackerTotal - _result.DefenderTotal;
                var lines = new List<string>
                {
                    $"Round {_battle.Round} — {_battle.Party.Goal}."
                };
                lines.AddRange(_heroicLines);
                lines.AddRange(_defenderMagic.Lines);
                lines.AddRange(_magic.Lines);
                lines.Add($"Attackers d100 {_result.AttackerRoll} + Strength {_strength} = {_result.AttackerTotal}.");
                lines.Add($"Defenders d100 {_result.DefenderRoll} + Defense {_defense} = {_result.DefenderTotal}.");
                lines.Add(diff == 0
                    ? "Tie. Both sides take a glancing hit."
                    : _result.AttackerWon
                        ? $"Attackers win by {_result.Margin}."
                        : $"Defenders win by {_result.Margin}.");
                if (_magic.BuffSteps > 0)
                {
                    lines.Add(_band == _originalBand
                        ? "Buff/debuff spells cannot shift the casualty table further."
                        : $"Casualty table shifts {_magic.BuffSteps} step(s) toward the attackers.");
                }
                if (_holdTheLine)
                    lines.Add("Hold the Line: defender a→b, b→c, c ignored; aa→bb, bb→cc, cc ignored. Raiders gain no ground.");
                if (_defenderMagic.BarrierActive)
                    lines.Add("Defensive Barrier: b, c, bb and cc do not count against the defenders.");
                if (_magic.AoeCount > 0)
                    lines.Add(DescribeAoeAgainstDefenders(_magic.AoeCount, _defenderMagic.BarrierActive, _holdTheLine));
                if (_defenderMagic.FireballCount > 0)
                    lines.Add($"Fireball adds {_defenderMagic.FireballCount}×(1+aa+bb+cc) to raider casualties.");
                if (_healed > 0)
                    lines.Add($"Heal spells negate {_healed} raider casualty hit(s).");
                if (_swing != null)
                    lines.Add(_swing.Describe());

                if (_attackerHits > 0)
                    lines.Add(ApplyAttackerDamage(_battle, _attackerHits));
                if (DefenderCasualties.Count > 0)
                    lines.Add(ApplyProjectedDefenderCasualties(_stronghold, _battle, DefenderCasualties));

                bool raidersAlive = _battle.LivingCount > 0;
                if (_goalTicks > 0 && raidersAlive)
                    lines.Add(ApplyGoalTicks(_battle, _stronghold, _goalTicks, _result.Margin));
                else if (_goalTicks > 0)
                    lines.Add("The raiders break before they can press their goal.");

                RefreshLiveStats(_battle.Party, _faction);
                var after = Calculate(_stronghold);
                lines.Add($"Raiders: {_battle.LivingCount}/{_battle.StartingNumbers} left, Strength {_battle.Party.Stats.Strength}, HP {_battle.Party.Stats.HitPoints}.");
                lines.Add($"Defense now {after.TotalDefense} (buildings {after.BuildingDefense} + fighters {after.NpcCombat}).");

                CheckEndOfRound(_battle);
                _result.Ended = _battle.Ended;
                _result.EndReason = _battle.EndReason;
                if (_battle.Ended)
                    lines.Add(DescribeEnd(_battle));

                _result.Report = string.Join(Environment.NewLine, lines.Where(l => !string.IsNullOrWhiteSpace(l)));
                _battle.RoundReports.Add(_result.Report);
                _battle.Log.Add(_result.Report);
                _battle.Log.Add(string.Empty);
                return _result;
            }

            private void RecalculateTotals()
            {
                _result.AttackerTotal = _result.AttackerRoll + _strength;
                _result.DefenderTotal = _result.DefenderRoll + _defense;
                int diff = _result.AttackerTotal - _result.DefenderTotal;
                _result.Margin = Math.Abs(diff);
                _result.AttackerWon = diff > 0;
            }
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

        // a, b and c are rolled once and reused for the whole round. aa, bb and cc use the
        // same odds but are rolled fresh on every single use, so the same term appearing
        // twice in a formula means two separate rolls.
        private sealed class SwingContext
        {
            private const int AOdds = 75;
            private const int BOdds = 50;
            private const int COdds = 25;

            private readonly List<string> _extraRolls = new();

            public int A { get; }
            public int B { get; }
            public int C { get; }

            public SwingContext()
            {
                A = Random.Shared.Next(1, 101);
                B = Random.Shared.Next(1, 101);
                C = Random.Shared.Next(1, 101);
            }

            public bool AHit => A <= AOdds;
            public bool BHit => B <= BOdds;
            public bool CHit => C <= COdds;

            public int AA() => RollExtra("aa", AOdds);
            public int BB() => RollExtra("bb", BOdds);
            public int CC() => RollExtra("cc", COdds);

            private int RollExtra(string label, int odds)
            {
                int roll = Random.Shared.Next(1, 101);
                bool hit = roll <= odds;
                _extraRolls.Add($"{label} {roll}{(hit ? " hit" : " miss")}");
                return hit ? 1 : 0;
            }

            public string Describe()
            {
                string baseLine = $"Swing: a {A}{(AHit ? " hit" : " miss")} (75%), b {B}{(BHit ? " hit" : " miss")} (50%), c {C}{(CHit ? " hit" : " miss")} (25%).";
                if (_extraRolls.Count == 0)
                    return baseLine;
                return baseLine + $" Individual rolls: {string.Join(", ", _extraRolls)}.";
            }
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

        private sealed class DefenderMagic
        {
            public List<string> Lines { get; } = new();
            public int ExtraAttackerHits { get; set; }
            public int FireballCount { get; set; }
            public bool BarrierActive { get; set; }
        }

        // Spells the DM declared for the defenders before the rolls were entered. Spell
        // points were already deducted at declaration time.
        private static DefenderMagic ResolveDefenderMagic(RaidBattle battle)
        {
            var magic = new DefenderMagic();
            if (battle.PendingDefenderSpells == null || battle.PendingDefenderSpells.Count == 0)
                return magic;

            foreach (var cast in battle.PendingDefenderSpells)
            {
                var def = DefenderSpellCatalog.Get(cast.Spell);
                magic.Lines.Add($"{cast.CasterName} casts {def.Name} ({def.Effect})");

                switch (cast.Spell)
                {
                    case DefenderSpell.MagicMissile:
                        magic.ExtraAttackerHits++;
                        break;
                    case DefenderSpell.DefensiveBarrier:
                        magic.BarrierActive = true;
                        break;
                    case DefenderSpell.Fireball:
                        magic.FireballCount++;
                        break;
                    case DefenderSpell.MassDebuff:
                        // Already applied to the attacker d100 when the roll was taken.
                        break;
                }
            }

            battle.PendingDefenderSpells.Clear();
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

        // A casualty entry from the table: a flat count, which of the once-per-round dice
        // apply, and how many separate rolls of each per-use die to make.
        private readonly struct CasualtyTerms
        {
            public int Flat { get; init; }
            public bool A { get; init; }
            public bool B { get; init; }
            public bool C { get; init; }
            public int AA { get; init; }
            public int BB { get; init; }
            public int CC { get; init; }
        }

        // Defensive Barrier strips b, c, bb and cc from the defenders' own casualties.
        // Hold the Line steps each defender die down one grade before that: a→b, b→c,
        // c ignored; aa→bb, bb→cc, cc ignored. Barrier then applies to the remapped die.
        private static int Resolve(CasualtyTerms terms, SwingContext swing, bool barrier, bool holdTheLine = false)
        {
            int hits = terms.Flat;

            if (holdTheLine)
            {
                if (terms.A && !barrier && swing.BHit) hits++;
                if (terms.B && !barrier && swing.CHit) hits++;

                for (int i = 0; i < terms.AA; i++)
                {
                    if (!barrier) hits += swing.BB();
                }
                if (!barrier)
                {
                    for (int i = 0; i < terms.BB; i++) hits += swing.CC();
                }
                return hits;
            }

            if (terms.A && swing.AHit) hits++;
            if (!barrier && terms.B && swing.BHit) hits++;
            if (!barrier && terms.C && swing.CHit) hits++;

            for (int i = 0; i < terms.AA; i++) hits += swing.AA();
            if (!barrier)
            {
                for (int i = 0; i < terms.BB; i++) hits += swing.BB();
                for (int i = 0; i < terms.CC; i++) hits += swing.CC();
            }

            return hits;
        }

        // Enemy AoE is aa+bb+cc against defenders. Hold the Line remaps that the same
        // way as the casualty table; the barrier still strips b-grade dice.
        private static int ResolveAoeAgainstDefenders(SwingContext swing, bool barrier, bool holdTheLine)
        {
            if (holdTheLine)
            {
                if (barrier) return 0;
                return swing.BB() + swing.CC();
            }

            int hits = swing.AA();
            if (!barrier) hits += swing.BB() + swing.CC();
            return hits;
        }

        private static string DescribeAoeAgainstDefenders(int count, bool barrier, bool holdTheLine)
        {
            if (holdTheLine && barrier)
                return $"AoE adds nothing to defender casualties (Hold the Line remaps to bb/cc; barrier blocks both).";
            if (holdTheLine)
                return $"AoE adds {count}×(bb+cc) to defender casualties (Hold the Line; cc dropped).";
            if (barrier)
                return $"AoE adds {count}×aa to defender casualties (barrier blocks bb and cc).";
            return $"AoE adds {count}×(aa+bb+cc) to defender casualties.";
        }

        private static CasualtyTerms LoserTerms(int margin)
        {
            if (margin <= 4)
                return new CasualtyTerms { Flat = 1, A = true, B = true, C = true };
            if (margin <= 9)
                return new CasualtyTerms { Flat = 1, A = true, B = true, C = true, BB = 1, CC = 1 };
            if (margin <= 14)
                return new CasualtyTerms { Flat = 2, A = true, B = true, C = true, AA = 1 };
            if (margin <= 19)
                return new CasualtyTerms { Flat = 3, A = true, B = true, C = true, AA = 1, BB = 1 };
            if (margin <= 24)
                return new CasualtyTerms { Flat = 3, A = true, B = true, C = true, AA = 2, BB = 1, CC = 1 };
            if (margin <= 29)
                return new CasualtyTerms { Flat = 4, A = true, B = true, C = true, AA = 2, BB = 1, CC = 1 };

            int flat = (margin + 4) / 5; // ceil(margin / 5)
            return new CasualtyTerms { Flat = flat, A = true, B = true, C = true, AA = 2, BB = 1, CC = 1 };
        }

        private static CasualtyTerms WinnerTerms(int margin)
        {
            if (margin <= 4) return new CasualtyTerms { A = true, B = true };
            if (margin <= 9) return new CasualtyTerms { B = true, C = true };
            if (margin <= 19) return new CasualtyTerms { B = true };
            return new CasualtyTerms { C = true };
        }

        private static void CasualtiesFromMargin(int margin, bool attackerWon, bool tie, SwingContext swing,
            bool defenderBarrier, bool holdTheLine, out int attackerHits, out int defenderHits)
        {
            if (tie)
            {
                var tieTerms = new CasualtyTerms { A = true, B = true };
                attackerHits = Resolve(tieTerms, swing, barrier: false);
                defenderHits = Resolve(tieTerms, swing, defenderBarrier, holdTheLine);
                return;
            }

            var loser = LoserTerms(margin);
            var winner = WinnerTerms(margin);

            if (attackerWon)
            {
                attackerHits = Resolve(winner, swing, barrier: false);
                defenderHits = Resolve(loser, swing, defenderBarrier, holdTheLine);
            }
            else
            {
                attackerHits = Resolve(loser, swing, barrier: false);
                defenderHits = Resolve(winner, swing, defenderBarrier, holdTheLine);
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

        private static List<ProjectedCasualty> ProjectDefenderCasualties(Stronghold stronghold, RaidBattle battle, int hits)
        {
            var projected = new List<ProjectedCasualty>();
            var capturedIds = new HashSet<string>(battle.PendingCaptures.Select(n => n.Id));
            var virtualLight = new HashSet<string>();
            var virtualGrave = new HashSet<string>();
            var virtualDead = new HashSet<string>();

            foreach (var npc in stronghold.NPCs.Where(n => n.IsAlive))
            {
                if (HasState(npc, NPCStateType.LightlyInjured)) virtualLight.Add(npc.Id);
                if (HasState(npc, NPCStateType.GravelyInjured)) virtualGrave.Add(npc.Id);
            }

            for (int i = 0; i < hits; i++)
            {
                var pool = CasualtyPool(stronghold, capturedIds)
                    .Where(n => !virtualDead.Contains(n.Id))
                    .ToList();
                if (pool.Count == 0)
                {
                    projected.Add(new ProjectedCasualty
                    {
                        Npc = new NPC(NPCType.Peasant, "no one"),
                        RawSeverity = "none",
                        Outcome = "none",
                        Note = "no one left to hit"
                    });
                    break;
                }

                var npc = pool[Random.Shared.Next(pool.Count)];
                var hit = ProjectOneCasualty(npc,
                    virtualLight.Contains(npc.Id),
                    virtualGrave.Contains(npc.Id));
                ApplyVirtual(hit, virtualLight, virtualGrave, virtualDead);
                projected.Add(hit);
            }

            return projected;
        }

        private static ProjectedCasualty ProjectOneCasualty(NPC npc, bool wasLight, bool wasGrave)
        {
            int roll = TraitService.ModifyInjuryRoll(npc, Random.Shared.Next(100));
            string raw = RawInjurySeverity(npc.Hero, roll);
            var (outcome, note) = ResolveInjuryOutcome(npc.Name, raw, wasLight, wasGrave);
            return new ProjectedCasualty
            {
                Npc = npc,
                RawSeverity = raw,
                Outcome = outcome,
                Note = note
            };
        }

        private static void RetargetCasualty(ProjectedCasualty hit, NPC hero, IEnumerable<ProjectedCasualty> earlierHits)
        {
            bool wasLight = HasState(hero, NPCStateType.LightlyInjured)
                || earlierHits.Any(h => h.Npc.Id == hero.Id && h.Outcome == "light");
            bool wasGrave = HasState(hero, NPCStateType.GravelyInjured)
                || earlierHits.Any(h => h.Npc.Id == hero.Id && h.Outcome == "grave");
            var (outcome, note) = ResolveInjuryOutcome(hero.Name, hit.RawSeverity, wasLight, wasGrave);
            hit.Npc = hero;
            hit.Outcome = outcome;
            hit.Note = note;
        }

        private static void ApplyVirtual(ProjectedCasualty hit, HashSet<string> light, HashSet<string> grave, HashSet<string> dead)
        {
            if (hit.Npc == null || hit.Outcome == "none") return;
            if (hit.Outcome == "death")
            {
                dead.Add(hit.Npc.Id);
                return;
            }
            if (hit.Outcome == "grave")
            {
                grave.Add(hit.Npc.Id);
                light.Remove(hit.Npc.Id);
                return;
            }
            if (hit.Outcome == "light")
                light.Add(hit.Npc.Id);
        }

        private static string RawInjurySeverity(bool hero, int roll)
        {
            if (hero)
            {
                if (roll < 50) return "light";
                if (roll < 85) return "grave";
                return "death";
            }

            if (roll < 25) return "light";
            if (roll < 50) return "grave";
            return "death";
        }

        private static (string Outcome, string Note) ResolveInjuryOutcome(string name, string raw, bool wasLight, bool wasGrave)
        {
            if (raw == "death" || (raw == "grave" && wasGrave))
                return ("death", $"{name} killed");
            if (raw == "light" && wasGrave)
                return ("none", $"{name} already gravely injured");
            if (raw == "grave" || (raw == "light" && wasLight))
                return ("grave", $"{name} gravely injured");
            return ("light", $"{name} lightly injured");
        }

        private static string ApplyProjectedDefenderCasualties(Stronghold stronghold, RaidBattle battle, List<ProjectedCasualty> hits)
        {
            var notes = new List<string>();
            foreach (var hit in hits)
            {
                if (hit.Npc == null || hit.Note == "no one left to hit")
                {
                    notes.Add("no one left to hit");
                    continue;
                }
                notes.Add(ApplyResolvedCasualty(stronghold, battle, hit));
            }

            return $"Defender casualties: {string.Join("; ", notes)}.";
        }

        private static string ApplyResolvedCasualty(Stronghold stronghold, RaidBattle battle, ProjectedCasualty hit)
        {
            var npc = hit.Npc;
            if (npc == null || !npc.IsAlive)
                return $"{hit.Note}";

            if (hit.Outcome == "death")
            {
                KillNpc(stronghold, battle, npc);
                return $"{npc.Name} killed";
            }

            if (hit.Outcome == "none")
                return hit.Note;

            if (hit.Outcome == "grave")
            {
                npc.AddHealthState(NPCStateType.GravelyInjured);
                return $"{npc.Name} gravely injured";
            }

            npc.AddHealthState(NPCStateType.LightlyInjured);
            return $"{npc.Name} lightly injured";
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
            var hit = ProjectOneCasualty(npc,
                HasState(npc, NPCStateType.LightlyInjured),
                HasState(npc, NPCStateType.GravelyInjured));
            return ApplyResolvedCasualty(stronghold, battle, hit);
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
