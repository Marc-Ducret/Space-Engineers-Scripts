
IMyTerminalBlock raws;
IMyTextPanel panel_good;
IMyTextPanel panel_ok;
IMyTextPanel panel_bad;

public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    init();
}

public void init() {
    raws = GridTerminalSystem.GetBlockWithName("Cargo Raw");
    panel_good = GridTerminalSystem.GetBlockWithName("Display Raw Good") as IMyTextPanel;
    panel_ok   = GridTerminalSystem.GetBlockWithName("Display Raw Ok") as IMyTextPanel;
    panel_bad  = GridTerminalSystem.GetBlockWithName("Display Raw Bad") as IMyTextPanel;
}

List<MyItemType> items = new List<MyItemType> {
    new MyItemType("MyObjectBuilder_Ore", "Iron"),
    new MyItemType("MyObjectBuilder_Ore", "Nickel"),
    new MyItemType("MyObjectBuilder_Ore", "Silicon"),
    new MyItemType("MyObjectBuilder_Ore", "Cobalt"),
    new MyItemType("MyObjectBuilder_Ore", "Gold"),
    new MyItemType("MyObjectBuilder_Ore", "Ice"),
    new MyItemType("MyObjectBuilder_Ore", "Scrap"),
    new MyItemType("MyObjectBuilder_Ore", "Stone"),
    new MyItemType("MyObjectBuilder_Ore", "Magnesium"),
    new MyItemType("MyObjectBuilder_Ore", "Organic"),
    new MyItemType("MyObjectBuilder_Ore", "Platinum"),
    new MyItemType("MyObjectBuilder_Ore", "Silver"),
    new MyItemType("MyObjectBuilder_Ore", "Uranium")
};

List<long> itemExpected = new List<long> {
    300000, // Iron
    15000,  // Nickel
    15000,  // Silicon
    15000,  // Cobalt
    0,      // Gold
    15000,  // Ice
    0,      // Scrap
    5000,   // Stone
    0,      // Magnesium
    0,      // Organic
    0,      // Platinum
    0,      // Silver
    0       // Uranium
};

public void Main(string argument, UpdateType updateSource) {
    IMyInventory inventory = raws.GetInventory();
    StringBuilder sb_good = new StringBuilder("", 100);
    StringBuilder sb_ok = new StringBuilder("", 100);
    StringBuilder sb_bad = new StringBuilder("", 100);
    
    for(int i = 0; i < items.Count(); ++i) {
        long count = inventory.GetItemAmount(items[i]).RawValue / 1000000;
        if(count >= itemExpected[i]) {
            sb_good.AppendFormat("{0} {1}/{2}\n", items[i].SubtypeId, count, itemExpected[i]);
        } else if(count >= itemExpected[i]/3) {
            sb_ok.AppendFormat("{0} {1}/{2}\n", items[i].SubtypeId, count, itemExpected[i]);
        } else {
            sb_bad.AppendFormat("{0} {1}/{2}\n", items[i].SubtypeId, count, itemExpected[i]);
        }
    }
    
    panel_good.WriteText(sb_good, false);
    panel_ok.WriteText(sb_ok, false);
    panel_bad.WriteText(sb_bad, false);
}
