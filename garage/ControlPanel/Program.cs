IMyTextSurface lcd;
IMySoundBlock sb;
List<IMyLightingBlock> lbs;
List<IMyShipWelder> welders;
bool status;

public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    init();
}

public void init() {
    lcd = GridTerminalSystem.GetBlockWithName("Garage - Corner LCD Bottom") as IMyTextSurface;
    lcd.Alignment = TextAlignment.CENTER;
    lcd.FontSize = 7.0f;
    sb = GridTerminalSystem.GetBlockWithName("Garage - Alarm") as IMySoundBlock;
    sb.SelectedSound = "Warning_Beep_1";
    sb.LoopPeriod = 3.0f;
    IMyBlockGroup lbs_group =
        GridTerminalSystem.GetBlockGroupWithName("Garage - Spotlights");
    List<IMyTerminalBlock> lb_list = new List<IMyTerminalBlock>();
    lbs_group.GetBlocks(lb_list);
    lbs = new List<IMyLightingBlock>();
    foreach(var lb in lb_list) {
        lbs.Add((IMyLightingBlock)lb);
    }
    foreach(var lb in lbs) {
        lb.BlinkLength = 0.8f;
    }
    IMyBlockGroup welders_group =
        GridTerminalSystem.GetBlockGroupWithName("Garage - Welders");
    List<IMyTerminalBlock> welder_list = new List<IMyTerminalBlock>();
    welders_group.GetBlocks(welder_list);
    welders = new List<IMyShipWelder>();
    foreach(var welder in welder_list) {
        welders.Add((IMyShipWelder)welder);
    }
    foreach(var welder in welders) {
        status |= welder.Enabled;
        if(status) break;
    }
}

public void Main(string argument, UpdateType updateSource) {
    bool is_one_on = false;
    foreach(var welder in welders) {
        is_one_on |= welder.Enabled;
        if(is_one_on) break;
    }
    if(is_one_on) {
        lcd.BackgroundColor = Color.DarkRed;
        lcd.WriteText("Welders ON --- DANGER!", false);
        sb.Play();
        if(status ^ is_one_on) {
            foreach(var lb in lbs) {
                lb.Color = Color.DarkRed;
                lb.BlinkIntervalSeconds = 3.0f;
            }
        }
    } else {
        lcd.BackgroundColor = Color.ForestGreen;
        lcd.WriteText("Welders OFF", false);
        if(status ^ is_one_on) {
            sb.Stop();
            foreach(var lb in lbs) {
                lb.Color = Color.ForestGreen;
                lb.BlinkIntervalSeconds = 0.0f;
            }
        }
    }
    status = is_one_on;
}

