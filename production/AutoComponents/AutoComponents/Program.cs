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
        // Specification
        const String factoryGroupName = "Auto Assemblers";
        const String displayPrefix = "Display Component";
        const String cargoName = "Cargo Components";
        const String objectType = "MyObjectBuilder_Component";
        static MyFixedPoint batchSize = fromLong(100);

        static List<Target> targets = new List<Target>
        {
            new Target("BulletproofGlass", 500, "BulletproofGlass"),
            new Target("Computer", 1000, "ComputerComponent"),
            new Target("Construction", 5000, "ConstructionComponent"),
            new Target("Detector", 0, "DetectorComponent"),
            new Target("Display", 1000, "Display"),
            new Target("Girder", 1000, "GirderComponent"),
            new Target("InteriorPlate", 5000, "InteriorPlate"),
            new Target("LargeTube", 1000, "LargeTube"),
            new Target("MetalGrid", 1000, "MetalGrid"),
            new Target("Motor", 1000, "MotorComponent"),
            new Target("PowerCell", 1000, "PowerCell"),
            new Target("RadioCommunication", 500, "RadioCommunicationComponent"),
            new Target("Reactor", 0, "ReactorComponent"),
            new Target("SmallTube", 5000, "SmallTube"),
            new Target("SolarCell", 1000, "SolarCell"),
            new Target("SteelPlate", 10000, "SteelPlate"),
            new Target("Superconductor", 0, "Superconductor"),
            new Target("Thrust", 0, "ThrustComponent"),
            new Target("ZoneChip", 0, "ZoneChip"),
        };

        // Implementation
        List<IMyProductionBlock> factories;
        IMyTerminalBlock cargo;
        IMyTextPanel panel_good;
        IMyTextPanel panel_ok;
        IMyTextPanel panel_bad;
        int actualTarget;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            init();
        }

        public void init()
        {
            cargo = GridTerminalSystem.GetBlockWithName(cargoName);

            factories = new List<IMyProductionBlock>();
            IMyBlockGroup factory_group = GridTerminalSystem.GetBlockGroupWithName(factoryGroupName);
            factory_group.GetBlocksOfType(factories);

            panel_good = GridTerminalSystem.GetBlockWithName(displayPrefix + " Good") as IMyTextPanel;
            panel_ok = GridTerminalSystem.GetBlockWithName(displayPrefix + " Ok") as IMyTextPanel;
            panel_bad = GridTerminalSystem.GetBlockWithName(displayPrefix + " Bad") as IMyTextPanel;

            actualTarget = 0;
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
                target = new MyItemType(objectType, t);
                recipe = new MyDefinitionId();
                bool _ = MyDefinitionId.TryParse("MyObjectBuilder_BlueprintDefinition", r, out recipe);
                amount = fromLong(a);
            }

            public MyFixedPoint batch()
            {
                if (toLong(amount) == 0) return batchSize; 
                else return MyFixedPoint.Min(batchSize, fromLong(toLong(amount) / 4));
            }
        };

        public void Main(string argument, UpdateType updateSource)
        {
            var quantities = new Dictionary<MyItemType, MyFixedPoint>();

            // Init quantities from the content of the coffer
            IMyInventory inventory = cargo.GetInventory();
            foreach (Target target in targets)
            {
                quantities.Add(target.target, inventory.GetItemAmount(target.target));
            }

            var usage = new Dictionary<IMyProductionBlock, long>();
            foreach (IMyProductionBlock factory in factories)
            {
                List<MyProductionItem> queue = new List<MyProductionItem>();
                factory.GetQueue(queue);
                usage.Add(factory, queue.Count());
                foreach (MyProductionItem prod in queue)
                {
                    foreach (Target target in targets)
                    {
                        if (target.recipe != prod.BlueprintId) continue;
                        quantities[target.target] += prod.Amount;
                    }
                }
            }

            int actualFactory = 0;
            while (usage[factories[actualFactory]] > 3)
            {
                actualFactory++;
                // No free factory
                if (actualFactory >= factories.Count()) { goto Displays; }
            }
            while (targets[actualTarget].amount - quantities[targets[actualTarget].target] <= targets[actualTarget].batch())
            {
                actualTarget++;
                // Nothing to produce
                if (actualTarget >= targets.Count()) {
                    actualTarget = 0;
                    goto Displays;
                }
            }
            // Iterate over targets to produce, one queue item by one queue item,
            // and assign them to factories
            while (true)
            {
                factories[actualFactory].AddQueueItem(targets[actualTarget].recipe, targets[actualTarget].batch());
                usage[factories[actualFactory]]++;
                quantities[targets[actualTarget].target] += targets[actualTarget].batch();

                // Find next target
                int nextTarget = (actualTarget + 1) % targets.Count();
                while (targets[nextTarget].amount - quantities[targets[nextTarget].target] <= targets[nextTarget].batch())
                {
                    nextTarget++;
                    nextTarget %= targets.Count();
                    // No target left
                    if (nextTarget == actualTarget) { goto Displays; }
                }
                actualTarget = nextTarget;

                // Find next factory
                int nextFactory = (actualFactory + 1) % factories.Count();
                while (usage[factories[nextFactory]] > 3)
                {
                    nextFactory++;
                    nextFactory %= factories.Count();
                    // No free factory left
                    if (nextFactory == actualFactory) { goto Displays; }
                }
                actualFactory = nextFactory;
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
                long batch = toLong(target.batch());

                if (expected == 0 && bs == 0) continue;

                if (bs + staged + batch > expected && staged == 0)
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
