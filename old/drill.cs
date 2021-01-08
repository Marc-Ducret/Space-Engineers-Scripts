int state;
int piston;
const int nb_pistons = 1;

public Program() {
    state = 0;
}

public void Save(){
}

public void exec(IMyTerminalBlock tblock, string action) {
    var act = tblock.GetActionWithName(action);
    act.Apply(tblock);
}

public void Main(string argument, UpdateType updateSource) {
    var drills_group = GridTerminalSystem.GetBlockGroupWithName("Drills");
    var warningLights = GridTerminalSystem.GetBlockGroupWithName("Warning");
    var timer = GridTerminalSystem.GetBlockWithName("Timer");

    if(state == 0) {

      List<IMyTerminalBlock> drills = new List<IMyTerminalBlock>();
      drills_group.GetBlocks(drills);
      foreach(var drill in drills) {
        exec(drill, "OnOff_On");
      }

      var piston1 = GridTerminalSystem.GetBlockWithName("Piston 1") as IMyPistonBase;
      exec(piston1, "Extend");
      state = 1;
      piston = 1;
    } else if(state == 1) {
      var piston1 = GridTerminalSystem.GetBlockWithName("Piston " + piston) as IMyPistonBase;
      if(Math.Abs(piston1.CurrentPosition - piston1.MaxLimit) < 1e-5) {
        piston = piston + 1;
        if(piston <= nb_pistons) {
          var piston2 = GridTerminalSystem.GetBlockWithName("Piston " + piston) as IMyPistonBase;
          exec(piston2, "Extend");
        } else {
          state = 2;
        }
      }
    } else if(state == 2) {
      for(int i = 1; i <= nb_pistons; ++i) {
        var piston1 = GridTerminalSystem.GetBlockWithName("Piston " + i);
        exec(piston1, "Retract");
      }
      state = 3;
    } else if(state == 3) {
      var piston1 = GridTerminalSystem.GetBlockWithName("Piston 1") as IMyPistonBase;
      if(Math.Abs(piston1.CurrentPosition - piston1.MinLimit) < 1e-5) {
        List<IMyTerminalBlock> drills = new List<IMyTerminalBlock>();
        drills_group.GetBlocks(drills);
        foreach(var drill in drills) {
          exec(drill, "OnOff_Off");
        }

        state = 0;
      }
    }

    if(state != 0) {
        exec(timer, "Start");
    }

    List<IMyTerminalBlock> lights = new List<IMyTerminalBlock>();
    warningLights.GetBlocks(lights);
    foreach(var light in lights) {
        exec(light, state > 0 ? "OnOff_On" : "OnOff_Off");
    }
}
