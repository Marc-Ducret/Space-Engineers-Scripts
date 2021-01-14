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
        List<IMyAssembler> refineries;
        IMyTerminalBlock components;
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
            "Auto Assembler 1"
        };

        public void init()
        {
            components = GridTerminalSystem.GetBlockWithName("Cargo Components");

            refineries = new List<IMyAssembler>();
            foreach (String s in refinery_names)
            {
                refineries.Add(GridTerminalSystem.GetBlockWithName(s) as IMyAssembler);
            }

            panel_good = GridTerminalSystem.GetBlockWithName("Display Component Good") as IMyTextPanel;
            panel_ok = GridTerminalSystem.GetBlockWithName("Display Component Ok") as IMyTextPanel;
            panel_bad = GridTerminalSystem.GetBlockWithName("Display Component Bad") as IMyTextPanel;

            queueAmount = fromLong(100);
        }

        static MyFixedPoint fromLong(long v)
        {
            return new MyFixedPoint { RawValue = v * 1000000 };
        }
        static long toLong(MyFixedPoint p)
        {
            return p.RawValue / 1000000;
        }

        struct Target
        {
            public MyItemType target;
            public MyDefinitionId recipe;
            public MyFixedPoint amount;

            public Target(String t, long a, String r)
            {
                target = new MyItemType("MyObjectBuilder_Component", t);
                recipe = new MyDefinitionId();
                bool _ = MyDefinitionId.TryParse("MyObjectBuilder_BlueprintDefinition", r, out recipe);
                amount = fromLong(a);
            }
        };

        List<Target> targets = new List<Target>
        {
            new Target("BulletproofGlass", 200, "BulletproofGlass"),
            new Target("Computer", 500, "ComputerComponent"),
            new Target("Construction", 1000, "ConstructionComponent"),
            new Target("Detector", 0, "DetectorComponent"),
            new Target("Display", 500, "Display"),
            new Target("Girder", 200, "GirderComponent"),
            new Target("InteriorPlate", 2000, "InteriorPlate"),
            new Target("LargeTube", 200, "LargeTube"),
            new Target("MetalGrid", 200, "MetalGrid"),
            new Target("Motor", 500, "MotorComponent"),
            new Target("PowerCell", 100, "PowerCell"),
            new Target("RadioCommunication", 100, "RadioCommunicationComponent"),
            new Target("Reactor", 0, "ReactorComponent"),
            new Target("SmallTube", 500, "SmallTube"),
            new Target("SolarCell", 100, "SolarCell"),
            new Target("SteelPlate", 2000, "SteelPlate"),
            new Target("Superconductor", 0, "Superconductor"),
            new Target("Thrust", 0, "ThrustComponent"),
            new Target("ZoneChip", 0, "ZoneChip"),
        };

        public void Main(string argument, UpdateType updateSource)
        {
            var quantities = new Dictionary<MyItemType, MyFixedPoint>();

            // Init quantities from the content of the coffer
            IMyInventory inventory = components.GetInventory();
            foreach (Target target in targets)
            {
                quantities.Add(target.target, inventory.GetItemAmount(target.target));
            }

            var usage = new Dictionary<IMyAssembler, long>();
            foreach (IMyAssembler refinery in refineries)
            {
                List<MyProductionItem> queue = new List<MyProductionItem>();
                refinery.GetQueue(queue);
                usage.Add(refinery, queue.Count());
                foreach (MyProductionItem prod in queue)
                {
                    foreach (Target target in targets)
                    {
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

            foreach (Target target in targets)
            {
                long bs = toLong(inventory.GetItemAmount(target.target));
                long staged = toLong(quantities[target.target]) - bs;
                long expected = toLong(target.amount);
                if (bs + staged >= expected)
                {
                    sb_good.AppendFormat("{0} {1}/{2}/{3}\n", target.target.SubtypeId, bs, staged, expected);
                }
                else if (bs + staged >= expected / 3)
                {
                    sb_ok.AppendFormat("{0} {1}/{2}/{3}\n", target.target.SubtypeId, bs, staged, expected);
                }
                else
                {
                    sb_bad.AppendFormat("{0} {1}/{2}/{3}\n", target.target.SubtypeId, bs, staged, expected);
                }
            }

            panel_good.WriteText(sb_good, false);
            panel_ok.WriteText(sb_ok, false);
            panel_bad.WriteText(sb_bad, false);
        }
    }
}
