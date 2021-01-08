Vector3D destination;
bool docking;

public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    docking = false;
}

public void Save() {

}

float extendFactor = 1F;
float margin = 3F;
float retractSpeed = 4;

public void Main(string argument, UpdateType updateSource) {
    if(argument.Length > 0) {
        Vector3D.TryParse(argument, out destination);
        Echo("Dock to "+destination.ToString());
        docking = true;
    } else {
        var connectorBase = GridTerminalSystem.GetBlockWithName("Connector Miner") as IMyShipConnector;
        var transform = connectorBase.WorldMatrix;
        var pistonForward = GridTerminalSystem.GetBlockWithName("Piston Dock Miner Forward") as IMyPistonBase;
        var pistonLeft = GridTerminalSystem.GetBlockWithName("Piston Dock Miner Left") as IMyPistonBase;
        var pistonUp = GridTerminalSystem.GetBlockWithName("Piston Dock Miner Up") as IMyPistonBase;

        if(docking) {
            Vector3D dir = destination - connectorBase.GetPosition();

            float forward = (float) Vector3D.Dot(transform.Forward, dir) - margin;
            float left = (float) Vector3D.Dot(transform.Right, dir);
            float up = (float) Vector3D.Dot(transform.Down, dir);
            pistonForward.Velocity = forward * extendFactor;
            pistonLeft.Velocity = left * extendFactor;
            pistonUp.Velocity = up * extendFactor;

            Echo("Forward="+forward);
            Echo("Left="+left);
            Echo("Up="+up);

            if(connectorBase.Status == MyShipConnectorStatus.Connectable) {
                connectorBase.Connect();
                docking = false;
            }
        } else if(connectorBase.Status != MyShipConnectorStatus.Connected) {
            Echo("Retract");
            pistonForward.Velocity = -retractSpeed;
            pistonLeft.Velocity = -retractSpeed;
            pistonUp.Velocity = -retractSpeed;
        } else {
            Echo("Idle");
            pistonForward.Velocity = 0;
            pistonLeft.Velocity = 0;
            pistonUp.Velocity = 0;
        }
    }
}
