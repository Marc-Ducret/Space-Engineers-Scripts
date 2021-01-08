float bestAngle;
float bestOutput;

float velocityFactor = 10F;

public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    bestAngle = 0;
    bestOutput = 0;
}

public void Save() {}

public void Main(string argument, UpdateType updateSource) {
    var sensor = GridTerminalSystem.GetBlockWithName("Solar Panel Sensor") as IMySolarPanel;
    var rotorSensor = GridTerminalSystem.GetBlockWithName("Solar Rotor Sensor") as IMyMotorStator;
    var rotorProduction = GridTerminalSystem.GetBlockWithName("Solar Rotor Production") as IMyMotorStator;

    bestOutput *= .9997F;

    if(sensor.MaxOutput > bestOutput) {
        bestOutput = sensor.MaxOutput;
        bestAngle = 2 * (float) Math.PI - rotorSensor.Angle;
    }
    var dir = (((bestAngle % Math.PI) - (rotorProduction.Angle % Math.PI) + Math.PI)  %  (2 * Math.PI)) - Math.PI;
    rotorProduction.TargetVelocityRPM = velocityFactor * (float) dir;

    Echo("SensorO: "+sensor.MaxOutput);
    Echo("BestOut: "+bestOutput);
    Echo("BestAngle: "+bestAngle);
    Echo("CurrAngle: "+rotorProduction.Angle);
    Echo("Velocity: "+rotorProduction.TargetVelocityRPM);
}
