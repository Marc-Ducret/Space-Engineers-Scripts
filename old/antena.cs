public Program() {

}

public void Save() {

}

public void Main(string argument, UpdateType updateSource) {
    var antenna = GridTerminalSystem.GetBlockWithName("Antenna") as IMyRadioAntenna;
    antenna.TransmitMessage(argument, MyTransmitTarget.Everyone);
}
