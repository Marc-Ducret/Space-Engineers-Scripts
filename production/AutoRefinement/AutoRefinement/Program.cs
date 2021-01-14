using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        List<IMyRefinery> refineries;
        IMyTerminalBlock raws;
        IMyTerminalBlock ingots;
        IMyTextPanel panel_good;
        IMyTextPanel panel_ok;
        IMyTextPanel panel_bad;

        // How much to queue at once of one component in a producer
        MyFixedPoint queueAmount;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            init();
        }

        List<String> refinery_names = new List<String> {
            "Auto Refinery 1"
        };

        public void init() {
            raws = GridTerminalSystem.GetBlockWithName("Cargo Raw");
            ingots = GridTerminalSystem.GetBlockWithName("Cargo Ingots");

            refineries = new List<IMyRefinery>();
            foreach(String s in refinery_names) {
                refineries.Add(GridTerminalSystem.GetBlockWithName(s) as IMyRefinery);
            }

            panel_good = GridTerminalSystem.GetBlockWithName("Display Ingot Good") as IMyTextPanel;
            panel_ok   = GridTerminalSystem.GetBlockWithName("Display Ingot Ok") as IMyTextPanel;
            panel_bad  = GridTerminalSystem.GetBlockWithName("Display Ingot Bad") as IMyTextPanel;

            queueAmount = fromLong(1000);
        }

        static MyFixedPoint fromLong(long v) {
            return new MyFixedPoint { RawValue = v * 1000000 };
        }
        static long toLong(MyFixedPoint p) {
            return p.RawValue / 1000000;
        }

        struct Target
        {
            public MyItemType target;
            public MyDefinitionId recipe;
            public MyFixedPoint amount;

            public Target(String t, long a, String r)
            {
                target = new MyItemType("MyObjectBuilder_Ingot", t);
                recipe = new MyDefinitionId();
                bool _ = MyDefinitionId.TryParse("MyObjectBuilder_BlueprintDefinition", r, out recipe);
                amount = fromLong(a);
            }
        };

        List<Target> targets = new List<Target>
        {
            new Target("Cobalt", 0, "CobaltOreToIngot"),
            new Target("Gold", 0, "GoldOreToIngot"),
            new Target("Iron", 50000, "IronOreToIngot"),
            new Target("Magnesium", 0, "MagnesiumOreToIngot"),
            new Target("Nickel", 10000, "NickelOreToIngot"),
            new Target("Platinum", 0, "PlatinumOreToIngot"),
            new Target("Silicon", 5000, "SiliconOreToIngot"),
            new Target("Silver", 0, "SilverOreToIngot"),
            new Target("Uranium", 0, "UraniumOreToIngot"),
        };

        public void Main(string argument, UpdateType updateSource)
        {
            var quantities = new Dictionary<MyItemType, MyFixedPoint>();

            // Init quantities from the content of the coffer
            IMyInventory inventory = ingots.GetInventory();
            foreach(Target target in targets) {
                quantities.Add(target.target, inventory.GetItemAmount(target.target));
            }

            var usage = new Dictionary<IMyRefinery, long>();
            foreach(IMyRefinery refinery in refineries) {
                List<MyProductionItem> queue = new List<MyProductionItem>();
                refinery.GetQueue(queue);
                usage.Add(refinery, queue.Count());
                foreach(MyProductionItem prod in queue) {
                    foreach(Target target in targets) {
                        if (target.recipe != prod.BlueprintId) continue;
                        quantities[target.target] += prod.Amount;
                    }
                }
            }

            int actualRefinery = 0;
            while (usage[refineries[actualRefinery]] > 3)
            {
                actualRefinery++;
                // No free refinery
                if (actualRefinery >= refineries.Count()) { goto Displays; }
            }
            int actualTarget = 0;
            while (targets[actualTarget].amount - quantities[targets[actualTarget].target] <= queueAmount)
            {
                actualTarget++;
                // Nothing to produce
                if (actualTarget >= targets.Count()) { goto Displays; }
            }
            // Iterate over targets to produce, one queue item by one queue item,
            // and assign them to refiniries
            while (true)
            {
                refineries[actualRefinery].AddQueueItem(targets[actualTarget].recipe, queueAmount);
                usage[refineries[actualRefinery]]++;
                quantities[targets[actualTarget].target] += queueAmount;

                // Find next target
                int nextTarget = (actualTarget + 1) % targets.Count();
                while (targets[nextTarget].amount - quantities[targets[nextTarget].target] <= queueAmount)
                {
                    nextTarget++;
                    nextTarget %= targets.Count();
                    // No target left
                    if (nextTarget == actualTarget) { goto Displays; }
                }
                actualTarget = nextTarget;

                // Find next refinery
                int nextRefinery = (actualRefinery + 1) % refineries.Count();
                while (usage[refineries[nextRefinery]] > 3)
                {
                    nextRefinery++;
                    nextRefinery %= refineries.Count();
                    // No free refinery left
                    if (nextRefinery == actualRefinery) { goto Displays; }
                }
                actualRefinery = nextRefinery;
            }

            // Update displays
        Displays:
            StringBuilder sb_good = new StringBuilder("", 100);
            StringBuilder sb_ok = new StringBuilder("", 100);
            StringBuilder sb_bad = new StringBuilder("", 100);

            foreach(Target target in targets) {
                long bs = toLong(inventory.GetItemAmount(target.target));
                long staged = toLong(quantities[target.target]) - bs;
                long expected = toLong(target.amount);
                if(bs + staged >= expected) {
                    sb_good.AppendFormat("{0} {1}/{2}/{3}\n", target.target.SubtypeId, bs, staged, expected);
                } else if (bs + staged >= expected / 3) {
                    sb_ok.AppendFormat("{0} {1}/{2}/{3}\n", target.target.SubtypeId, bs, staged, expected);
                } else {
                    sb_bad.AppendFormat("{0} {1}/{2}/{3}\n", target.target.SubtypeId, bs, staged, expected);
                }
            }

            panel_good.WriteText(sb_good, false);
            panel_ok.WriteText(sb_ok, false);
            panel_bad.WriteText(sb_bad, false);
        }
    }
}
