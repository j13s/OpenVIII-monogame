﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using OpenVIII.IGMData.Pool;
using OpenVIII.Kernel;
using Magic = OpenVIII.Battle.Dat.Magic;

namespace OpenVIII.IGMData.Target
{
    public class Group : IGMData.Group.Base
    {
        #region Fields

        private readonly int[] Renzokuken_hits = { 4, 5, 6, 7 };
        private IReadOnlyDictionary<int, Func<bool>> CommandFunc;
        private bool skipend;

        #endregion Fields

        #region Properties

        public BlueMagicQuistisLimitBreak BlueMagic { get; private set; }

        public BattleCommand Command { get; private set; }
        public Combine.KernelItem CombineKernelItem { get; private set; }
        public EnemyAttacksData EnemyAttack { get; private set; }
        public ItemInMenu Item { get; private set; }

        public MagicData Magic { get; private set; }
        public Random RandomTarget { get; private set; } = false;
        public int Casts { get; private set; }
        public Kernel.Target Target { get; private set; }

        private Draw Draw_Pool => (Draw)ITEM[2, 0];

        private Enemies TargetEnemies => (Enemies)ITEM[0, 0];

        private Party TargetParty => (Party)ITEM[1, 0];

        #endregion Properties

        #region Methods

        public static Group Create(Damageable damageable, bool makesubs = true)
        {
            const int X1 = 32;
            const int Width1 = 400;
            const int Height = 140;
            const int X2 = X1 + Width1;
            const int Width2 = 180;
            const int Y = 464;

            var r = Create<Group>(
                Enemies.Create(new Rectangle(X1, Y, Width1, Height)),
                Party.Create(new Rectangle(X2, Y, Width2, Height)),
                makesubs ? Pool.Draw.Create(new Rectangle(X1 + 50, Y - 50, 300, 40*3), damageable, true) : null);
            r.SetDamageable(damageable);
            r.CONTAINER.Pos = new Rectangle(X1, Y, Width1 + Width2, Height);
            r.after();
            return r;
        }

        public override void Draw() => base.Draw();

        public override bool Inputs()
        {
            var ret = false;
            if (Draw_Pool?.Enabled ?? false)
            {
                TargetEnemies.Cursor_Status |= Cursor_Status.Blinking;
                return Draw_Pool.Inputs();
            }

            TargetEnemies.Cursor_Status &= ~Cursor_Status.Blinking;
            if (TargetEnemies.Enabled && (((TargetEnemies.Cursor_Status | TargetParty.Cursor_Status) & Cursor_Status.Enabled) == 0 || !TargetParty.Enabled))
                TargetEnemies.Cursor_Status |= Cursor_Status.Enabled;
            else if (TargetParty.Enabled && (((TargetEnemies.Cursor_Status | TargetParty.Cursor_Status) & Cursor_Status.Enabled) == 0 || !TargetEnemies.Enabled))
                TargetParty.Cursor_Status |= Cursor_Status.Enabled;

            if (TargetEnemies.Enabled && ((TargetEnemies.Cursor_Status & Cursor_Status.Enabled) != 0 || TargetEnemies.CONTAINER.Pos.Contains(MouseLocation)))
            {
                TargetEnemies.Cursor_Status |= Cursor_Status.Enabled;
                TargetParty.Cursor_Status &= ~Cursor_Status.Enabled;
                ret = TargetEnemies.Inputs();
            }
            if (!ret && TargetParty.Enabled && ((TargetParty.Cursor_Status & Cursor_Status.Enabled) != 0 || TargetParty.CONTAINER.Pos.Contains(MouseLocation)))
            {
                TargetParty.Cursor_Status |= Cursor_Status.Enabled;
                TargetEnemies.Cursor_Status &= ~Cursor_Status.Enabled;
                ret = TargetParty.Inputs();
            }
            if (!ret)
            {
                Cursor_Status = Cursor_Status.Hidden | Cursor_Status.Static | Cursor_Status.Enabled;
                skipdata = true;
                ret = base.Inputs();
                skipdata = false;
            }
            return ret;
        }

        public override bool Inputs_CANCEL()
        {
            Hide();
            return true;
        }

        public override bool Inputs_OKAY()
        {
            base.Inputs_OKAY();
            return Execute();
        }

        /// <summary>
        /// Execute the ability on the Target. If Random is set execute on random target.
        /// </summary>
        /// <returns></returns>
        public bool Execute()
        {
            var ret = false;
            while (Casts-- > 0)
            {
                skipend = Casts > 0;
                if (CommandFunc.TryGetValue(Command.BattleID, out var val))
                    ret = val.Invoke() || ret;
                else
                    ret = CommandDefault() || ret;
            }
            return ret;
        }

        public void EndTurn()
        {
            if (!skipend)
                Damageable?.EndTurn();
        }

        public override void Refresh(Damageable damageable)
        {
            base.Refresh(damageable);
            Draw_Pool?.Refresh(damageable);
        }

        public override void Reset()
        {
            HideChildren();
            Hide();
            base.Reset();
        }

        public void SelectTargetWindows(Combine.KernelItem c)
        {
            var t = c.Target;
            SelectTargetWindows(t);
            Command = Memory.KernelBin.BattleCommands[19];
            CombineKernelItem = c;
        }

        public void SelectTargetWindows(EnemyAttacksData c)
        {
            // we don't know what the enemy attacks default target is. Setting a general default here.
            // The battle AI script sets the target for the enemies
            // http://forums.qhimm.com/index.php?topic=18384.0
            var t = Kernel.Target.Ally | Kernel.Target.Enemy | Kernel.Target.SingleTarget;
            SelectTargetWindows(t);
            Command = Memory.KernelBin.BattleCommands[1];
            EnemyAttack = c;
        }

        public void SelectTargetWindows(ItemInMenu c, bool shot = false)
        {
            var t = c.Battle?.Target ?? Kernel.Target.Enemy | Kernel.Target.SingleTarget;
            if (shot)
                t = c.Shot.Target;
            SelectTargetWindows(t);
            Command = Memory.KernelBin.BattleCommands[shot ? 14 : 4];
            Item = c;
        }

        public void SelectTargetWindows(BattleCommand c)
        {
            var t = c.Target;
            SelectTargetWindows(t);
            Command = c;
            Magic = null;
            BlueMagic = null;
        }

        public void SelectTargetWindows(MagicData c, int casts = 1, Random random = default)
        {
            var t = c.Target;
            SelectTargetWindows(t, casts, random);
            Command = Memory.KernelBin.BattleCommands[2];
            Magic = c;
        }

        public void SelectTargetWindows(BlueMagicQuistisLimitBreak c)
        {
            //not sure if target data is missing for blue magic.
            //The target box does show up in game so I imagine the target data is in there somewhere.
            var t = c.Target;
            SelectTargetWindows(t);
            Command = Memory.KernelBin.BattleCommands[15];
            BlueMagic = c;
        }

        public override void Show() => base.Show();

        public void ShowTargetWindows()
        {
            skipdata = true;
            Show();
            skipdata = false;
            Refresh();
        }

        protected override void Init()
        {
            base.Init();
            if (CommandFunc == null)
                CommandFunc = new Dictionary<int, Func<bool>>
                    {
                        //{0,Command00 },
                        {1,Command01_ATTACK },
                        {2,Command02_MAGIC },
                        //{3,Command03_GF },
                        {4,Command04_ITEM },
                        {5,Command05_RENZOKUKEN },
                        {6,Command06_DRAW },
                        {7,Command07_DEVOUR },
                        //{8,Command08_UNNAMED },
                        //{9,Command09_CAST },
                        //{10,Command10_STOCK },
                        {11,Command11_DUEL },
                        {12,Command12_MUG },
                        //{13,Command13_NOMSG },
                        {14,Command14_SHOT },
                        {15,Command15_BLUE_MAGIC },
                        //{16,Command16_SLOT },
                        {17,Command17_FIRE_CROSS_NO_MERCY },
                        {18,Command18_SORCERY_ICE_STRIKE },
                        {19,Command19_COMBINE },
                        {20,Command20_DESPERADO },
                        {21,Command21_BLOOD_PAIN },
                        {22,Command22_MASSIVE_ANCHOR },
                        //{23,Command23_DEFEND },
                        {24,Command24_MADRUSH },
                        {25,Command25_TREATMENT },
                        {26,Command26_RECOVERY },
                        {27,Command27_REVIVE },
                        {28,Command28_DARKSIDE },
                        {29,Command29_CARD },
                        {30,Command30_DOOM },
                        {31,Command31_KAMIKAZI },
                        {32,Command32_ABSORB },
                        {33,Command33_LVL_DOWN },
                        {34,Command34_LVL_UP },
                        {35,Command35_SINGLE },
                        {36,Command36_DOUBLE },
                        {37,Command37_TRIPLE },
                        {38,Command38_MINIMOG }
                    };

            //bool Command00() => throw new NotImplementedException();

            bool Command01_ATTACK()
            {
                NeededVariables(out var d);
                if (EnemyAttack != null && Damageable.GetEnemy(out var e))
                {
                    Debug.WriteLine($"{Damageable.Name} uses {EnemyAttack.Name}({EnemyAttack.MagicID}) enemy attack on { DebugMessageSuffix(d) }");
                }
                EndTurn();
                return true;
            }

            bool Command02_MAGIC()
            {
                NeededVariables(out var d, Magic.PositiveMagic);
                Debug.WriteLine($"{Damageable.Name} casts {Magic.Name}({Magic.MagicDataID}) spell on { DebugMessageSuffix(d) }");
                EndTurn();
                return true;
            }

            //bool Command03_GF() => throw new NotImplementedException();

            bool Command04_ITEM()
            {
                NeededVariables(out var d);
                Debug.WriteLine($"{Damageable.Name} uses {Item.Name}({Item.ID}) item on { DebugMessageSuffix(d) }");
                EndTurn();
                return true;
            }

            bool Command05_RENZOKUKEN()
            {
                NeededVariables(out var d);
                if (d.First().GetType() == typeof(Enemy) && Damageable.GetCharacterData(out var c))
                {
                    var squall = Memory.State[Characters.Squall_Leonhart];
                    //Renzokuken
                    var weaponid = squall.WeaponID;
                    var hits = 0;
                    if (c.CurrentCrisisLevel > 0)
                        hits = c.CurrentCrisisLevel < Renzokuken_hits.Length ? Renzokuken_hits[c.CurrentCrisisLevel] : Renzokuken_hits.Last();
                    //else return false;
                    else hits = Renzokuken_hits.First();
                    var finisherchance = (c.CurrentCrisisLevel + 1) * 60;
                    var willfinish = Memory.Random.Next(byte.MaxValue + 1) <= finisherchance;
                    var choosefinish = Memory.Random.Next(3 + 1);
                    var weapondata = Memory.KernelBin.WeaponsData[weaponid];
                    var renzokukenfinisher = weapondata.Renzokuken;
                    if (renzokukenfinisher == 0)
                        willfinish = false;

                    //per wiki the chance of which finisher is 25% each and the highest value finisher get the remaining of 100 percent.
                    //so rough divide is 100% when you only only have that
                    //when you unlock 2 one is 75% chance
                    //when you onlock 3 last one is 50%
                    //when you unlock all 4 it's 25% each.

                    //finishers each have their own target
                    Menu.BattleMenus.GetCurrentBattleMenu().Renzokuken.Reset(hits);
                    Menu.BattleMenus.GetCurrentBattleMenu().Renzokuken.Show();
                    if (willfinish)
                    {
                        var flags = Enum.GetValues(typeof(RenzokukenFinisher))
                            .Cast<RenzokukenFinisher>()
                            .Where(f => (f & renzokukenfinisher) != 0)
                            .ToList().AsReadOnly();
                        var finisher = choosefinish >= flags.Count ? flags.Last() : flags[choosefinish];
                        Debug.WriteLine($"{Damageable.Name} hits {hits} times with {Command.Name}({Command.BattleID}) then uses {Memory.KernelBin.RenzokukenFinishersData[finisher].Name}.");
                    }
                    else
                        Debug.WriteLine($"{Damageable.Name} hits {hits} times with {Command.Name}({Command.BattleID}) then fails to use a finisher.");
                }
                return true;
            }

            bool Command06_DRAW()
            {
                NeededVariables(out var d);
                //draw
                //spawn a 1 page 4 row pool of the magic/gfs that the selected enemy has.
                if (d.First().GetType() == typeof(Enemy))
                {
                    var e = (Enemy)d.First();
                    DrawMagic(e.DrawList);
                    Draw_Pool.Refresh(e.DrawList);
                    Draw_Pool.Show();
                }
                return true;
            }

            bool Command07_DEVOUR()
            {
                NeededVariables(out var d);
                //TODO add devour commands
                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            //bool Command08_UNNAMED() => throw new NotImplementedException();

            //bool Command09_CAST() => throw new NotImplementedException();

            //bool Command10_STOCK() => throw new NotImplementedException();

            bool Command11_DUEL()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command12_MUG()
            {
                NeededVariables(out var d);
                if (d.First().GetType() == typeof(Enemy))
                {
                    var e = (Enemy)d.First();
                    //unsure if party member being ejected or if they need to be in the party for rare item to work
                    var i = e.Mug(Damageable.SPD, Memory.State.PartyHasAbility(Abilities.RareItem));
                    Debug.WriteLine($"{Damageable.Name} stole {i.Data?.Name}({i.ID}) x {i.QTY} from { DebugMessageSuffix(d) }");
                }
                EndTurn();
                return true;
            }

            //bool Command13_NOMSG() => throw new NotImplementedException();

            bool Command14_SHOT()
            {
                NeededVariables(out var d);

                Menu.BattleMenus.GetCurrentBattleMenu().Shot.Refresh(Item,d);
                Menu.BattleMenus.GetCurrentBattleMenu().Shot.Show();

                Debug.WriteLine($"{Damageable.Name} uses {Item.Name}({Item.ID}) item on { DebugMessageSuffix(d) }");
                return true;
            }

            bool Command15_BLUE_MAGIC()
            {
                NeededVariables(out var d);
                Debug.WriteLine($"{Damageable.Name} casts {BlueMagic.Name}({BlueMagic.BlueMagic}) spell on { DebugMessageSuffix(d) }");
                EndTurn();
                return false;
            }

            //bool Command16_SLOT() => throw new NotImplementedException();

            bool Command17_FIRE_CROSS_NO_MERCY()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command18_SORCERY_ICE_STRIKE()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command19_COMBINE()
            {
                //perform angelo attack unless angel wing is unlocked and chosen in menu.
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {CombineKernelItem.Name}({CombineKernelItem.ID}) - Combine Limit Break on { DebugMessageSuffix(d) }");

                EndTurn();

                return true;
            }

            bool Command20_DESPERADO()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command21_BLOOD_PAIN()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command22_MASSIVE_ANCHOR()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            //bool Command23_DEFEND() => throw new NotImplementedException();

            bool Command24_MADRUSH()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command25_TREATMENT()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command26_RECOVERY()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command27_REVIVE()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }
            bool Command28_DARKSIDE()
            {
                NeededVariables(out var d);
                EndTurn();
                return true;
            }

            bool Command29_CARD()
            {
                NeededVariables(out var d);
                if (d.First().GetType() == typeof(Enemy))
                {
                    var e = (Enemy)d.First();
                    var c = e.Card();
                    if (c == Cards.ID.Fail)

                        Debug.WriteLine($"{Damageable.Name} Failed to use {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                    else if (c == Cards.ID.Immune)
                        Debug.WriteLine($"{Damageable.Name} Failed to use {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) } because they are immune!");
                    else
                        Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) } and got a {c} card");
                    EndTurn();
                }
                return true;
            }

            bool Command30_DOOM()
            {
                NeededVariables(out var d);
                EndTurn();
                return true;
            }

            bool Command31_KAMIKAZI()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command32_ABSORB()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command33_LVL_DOWN()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command34_LVL_UP()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }

            bool Command35_SINGLE() => Command02_MAGIC();

            bool Command36_DOUBLE()
            {
                // CHOOSE 2X TARGETS
                throw new NotImplementedException();
            }

            bool Command37_TRIPLE()
            {
                // CHOOSE 3X TARGETS
                throw new NotImplementedException();
            }
            bool Command38_MINIMOG()
            {
                NeededVariables(out var d);

                Debug.WriteLine($"{Damageable.Name} used {Command.Name}({Command.BattleID}) on { DebugMessageSuffix(d) }");
                EndTurn();

                return true;
            }
        }

        private void after()
        {
            TargetEnemies.Target_Party = TargetParty;
            TargetParty.Target_Enemies = TargetEnemies;
            Draw_Pool?.Hide();
            Hide();
        }

        private bool CommandDefault() => throw new NotImplementedException();

        private string DamageableNames(Damageable[] e)
        {
            var r = $"[{e[0].Name}";
            for (var j = 1; j < e.Length; j++)
                r += $", {e[j]}";
            r += "]";
            return r;
        }

        private void DebugMessageCommand(Enemies i, Damageable[] d, Damageable fromvc) =>
            Debug.WriteLine(
                $"{Damageable.Name} uses {Command.Name}({Command.BattleID}) command on {DebugMessageSuffix(d)}");

        private string DebugMessageSuffix(Damageable[] d) => $"{DamageableNames(d)}({(d.Length == 1 ? TargetEnemies.CURSOR_SELECT.ToString() : "MultiSelect")})";

        /// <summary>
        /// Display pool with list
        /// </summary>
        /// <param name="drawList"></param>
        private static void DrawMagic(IEnumerable<Magic> drawList) => Debug.WriteLine($"Display draw pool: {string.Join(", ", drawList)}");

        private void NeededVariables(out Damageable[] d, bool positive = false)
        {
            Damageable[] e = null;
            Damageable[] vc = null;
            var party = Memory.State.Party.Where(x => x != Characters.Blank).Select(y => Memory.State[y]);
            if (Target.HasFlag(Kernel.Target.SingleTarget))
            {
                if (TargetEnemies.Enabled && TargetParty.Enabled && RandomTarget.Single && RandomTarget.Side)
                {
                    var CombinedTargets = new List<Damageable>();

                    if (RandomTarget.PositiveMatters)
                    {
                        if (positive)
                            CombinedTargets.AddRange(party);
                        else
                            CombinedTargets.AddRange(Enemy.Party);
                    }
                    else
                    {
                        CombinedTargets.AddRange(Enemy.Party);
                        CombinedTargets.AddRange(party);
                    }
                    var rand = CombinedTargets.Random();
                    if (typeof(Enemy).Equals(rand.GetType()))
                    {
                        e = new[] { rand };
                    }
                    else if (typeof(Saves.CharacterData).Equals(rand.GetType()))
                    {
                        vc = new[] { rand };
                    }
                }
                else
                {
                    if (RandomTarget.Single)
                    {
                        TargetParty.Random();
                        TargetEnemies.Random();
                    }

                    e = new[] { Enemy.Party[TargetEnemies.CURSOR_SELECT < Enemy.Party.Count ? TargetEnemies.CURSOR_SELECT : Enemy.Party.Count - 1] };
                    vc = new[] { party.ElementAt(TargetParty.CURSOR_SELECT) };
                }
            }
            else
            {
                vc = party.ToArray();
                e = Enemy.Party.ToArray();

                if (RandomTarget.Side && TargetEnemies.Enabled && TargetParty.Enabled)
                {
                    if (RandomTarget.PositiveMatters)
                    {
                        if (positive)
                            e = null;
                        else
                            vc = null;
                    }
                    else
                        switch (Memory.Random.Next(2))
                        {
                            case 0:
                                vc = null;
                                break;

                            case 1:
                                e = null;
                                break;
                        }
                }
            }
            var c = Memory.State.PartyData.Where(x => x != Characters.Blank).ToList()[TargetParty.CURSOR_SELECT];
            var fromc = Menu.BattleMenus.GetDamageable();
            //fromvc = Memory.State.Party.Where(x => x != Characters.Blank).ToList()[p];
            if (RandomTarget.PositiveMatters)
            {
                if (positive)
                    e = null;
                else
                    vc = null;
            }
            d = vc;
            if (((TargetEnemies.Cursor_Status & Cursor_Status.Enabled) != 0 && TargetEnemies.Enabled) || d == null)
                d = e;
            DebugMessageCommand(TargetEnemies, d, Damageable);
        }

        private void SelectTargetWindows(Kernel.Target t, int casts = 1, Random random = default)
        {
            RandomTarget = random ?? new Random(false);
            Casts = casts;
            Target = t;
            if ((t & Kernel.Target.Ally) != 0 || t == Kernel.Target.None || ((t & Kernel.Target.Enemy) == 0 && (t & Kernel.Target.SingleSide) != 0))
            {
                TargetParty.Show();
                TargetAll(TargetParty);
            }
            else
                TargetParty.Hide();
            if ((t & Kernel.Target.Enemy) != 0)
            {
                TargetEnemies.Show();
                TargetAll(TargetEnemies);
            }
            else
                TargetEnemies.Hide();
            void TargetAll(Base i)
            {
                if (Target.HasFlag(Kernel.Target.SingleTarget))
                {
                    i.Cursor_Status &= ~Cursor_Status.All;
                }
                else
                    i.Cursor_Status |= Cursor_Status.All;
            }
            if (Damageable.GetEnemy(out var e))
            {
                if (TargetEnemies.Enabled == TargetParty.Enabled)
                {
                    //do nothing
                }
                else if (TargetEnemies.Enabled && !TargetParty.Enabled)
                {
                    TargetParty.Show();
                    TargetEnemies.Hide();
                }
                else if (!TargetEnemies.Enabled && TargetParty.Enabled)
                {
                    TargetParty.Hide();
                    TargetEnemies.Show();
                }
            }
        }

        #endregion Methods
    }
}
