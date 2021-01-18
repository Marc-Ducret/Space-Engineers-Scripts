
const String skeletonKitSuffix = "Skeleton Kit";

IMyTerminalBlock raws;
IMyTerminalBlock ingots;
IMyTerminalBlock components;
List<IMyTerminalBlock> blocks;

Dictionary<MyItemType, MyFixedPoint> skeletonItems = new Dictionary<MyItemType, MyFixedPoint>();

public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    init();
}

public void init() {
    raws = GridTerminalSystem.GetBlockWithName("Cargo Raw");
    ingots = GridTerminalSystem.GetBlockWithName("Cargo Ingots");
    components = GridTerminalSystem.GetBlockWithName("Cargo Components");
    
    blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocks(blocks);
    skeletonItems.Add(new MyItemType("MyObjectBuilder_Component", "SteelPlate"), 50);
    skeletonItems.Add(new MyItemType("MyObjectBuilder_Component", "InteriorPlate"), 50);
}

public Boolean isOre(MyInventoryItem item) {
    return item.Type.TypeId == "MyObjectBuilder_Ore";
}

public Boolean isIngot(MyInventoryItem item) {
    return item.Type.TypeId == "MyObjectBuilder_Ingot";
}

public Boolean isComponent(MyInventoryItem item) {
    return item.Type.TypeId == "MyObjectBuilder_Component"
        || item.Type.TypeId == "MyObjectBuilder_PhysicalGunObject";
}

MyItemType blockType(IMyTerminalBlock block) {
    var id = block.BlockDefinition;
    return new MyItemType(id.TypeIdString, id.SubtypeId);
}

List<MyItemType> oreBlacklist = new List<MyItemType> {
    new MyItemType("MyObjectBuilder_OxygenGenerator", ""),
    new MyItemType("MyObjectBuilder_OxygenGenerator", "OxygenGeneratorSmall"),
    new MyItemType("MyObjectBuilder_Refinery", "Blast Furnace"),
    new MyItemType("MyObjectBuilder_Refinery", "LargeRefinery")
};
List<MyItemType> ingotsBlacklist = new List<MyItemType> {
    new MyItemType("MyObjectBuilder_Assembler", "LargeAssembler"),
    new MyItemType("MyObjectBuilder_Assembler", "BasicAssembler")
};

public void Main(string argument, UpdateType updateSource) {
    MyFixedPoint amount;
    foreach(IMyTerminalBlock blk in blocks) {
        int inventories = blk.InventoryCount;
        List<MyInventoryItem> items = new List<MyInventoryItem>();

        for(int i = 0; i < inventories; i++) {
            IMyInventory inventory = blk.GetInventory(i);

            if(blk.EntityId != raws.EntityId && !oreBlacklist.Contains(blockType(blk))) {
                inventory.GetItems(items, isOre);
                foreach(MyInventoryItem item in items) {
                    inventory.TransferItemTo(raws.GetInventory(), item, null);
                }
            }
        
            if(blk.EntityId != ingots.EntityId && !ingotsBlacklist.Contains(blockType(blk))) {
                inventory.GetItems(items, isIngot);
                foreach(MyInventoryItem item in items) {
                    inventory.TransferItemTo(ingots.GetInventory(), item, null);
                }
            }
        
            if(blk.EntityId != components.EntityId 
                    && blk.CubeGrid.EntityId == components.CubeGrid.EntityId) {
                inventory.GetItems(items, isComponent);
                foreach(MyInventoryItem item in items) {
                    if(blk.CustomName.EndsWith(skeletonKitSuffix) &&
                            skeletonItems.TryGetValue(item.Type, out amount)) {
                        if(amount > inventory.GetItemAmount(item.Type)) {
                            amount -= inventory.GetItemAmount(item.Type);
                            MyInventoryItem? itemStorage = components.GetInventory().FindItem(item.Type);
                            if(itemStorage.HasValue) {
                                inventory.TransferItemFrom(components.GetInventory(), itemStorage.Value, amount);
                            }
                        } else {
                            amount = inventory.GetItemAmount(item.Type) - amount;
                            inventory.TransferItemTo(components.GetInventory(), item, amount);
                        }
                    } else {
                        inventory.TransferItemTo(components.GetInventory(), item, null);
                    }
                }
            }
        }

        if(blk.CustomName.EndsWith(skeletonKitSuffix)) {
            for(int i = 0; i < inventories; i++) {
                IMyInventory inventory = blk.GetInventory(i);
                foreach(var item in skeletonItems) {
                    if(item.Value > inventory.GetItemAmount(item.Key)) {
                        amount = item.Value - inventory.GetItemAmount(item.Key);
                        MyInventoryItem? itemStorage = components.GetInventory().FindItem(item.Key);
                        if(itemStorage.HasValue) {
                            inventory.TransferItemFrom(components.GetInventory(), itemStorage.Value, amount);
                        }
                    }
                }
            }
        }
    }
}
