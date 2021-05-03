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
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        int maxspeed = 5;

        double[] rotAngles = {0.05, 0.1, 0.2, 0.35, 0.5}; // cos of angle of inclination
        double[] accelerations = { 0.2, 0.5, 1, 2, 5 }; // m/s^2
        string[] speedNames = { "Dock", "Approach", "Normal", "Fast", "Very Fast" };

        int curSpeed;

        int tick = 0;

        void SetSpd(int spd)
        {
            if (spd >= maxspeed) spd = maxspeed - 1;
            if (spd < 0) spd = 0;
            curSpeed = spd;
        }

        double maxAltDrift = 1; // meters
        double maxSpdDrift = 0.5; // m/s

        // Maximum possible thrust
        double fullthmax = 250000;
        // Maximum possible thrust per thruster
        double thmax = 90000;

        double targAltitude;
        double targSpd;

        

        bool hDamp;
        bool manualAlt;
        bool vDamp;
        readonly IMyGyro g;
        readonly IMyShipController sc;
        readonly IMyTextPanel lcd;
        readonly List<IMyThrust> thrusters;
        readonly double mass;
        double lastAltitude;
        double backT;
        double frontT;
        double distback;
        double distfront;

        Vector3D lastHeading;

        public double GetAltitude()
        {
            double res = 0.0;
            sc.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out res);
            return res + 1550;
        }

        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            g = GridTerminalSystem.GetBlockWithName("Gyro") as IMyGyro;
            sc = GridTerminalSystem.GetBlockWithName("Cockpit") as IMyShipController;
            lcd = GridTerminalSystem.GetBlockWithName("LCD") as IMyTextPanel;
            thrusters = new List<IMyThrust>();
            GridTerminalSystem.GetBlockGroupWithName("Thrusters").GetBlocksOfType<IMyThrust>(thrusters);
            hDamp = false;
            manualAlt = true;
            vDamp = false;
            targAltitude = GetAltitude();
            mass = sc.CalculateShipMass().PhysicalMass;
            curSpeed = 0;
            lastHeading = sc.WorldMatrix.Forward;
        }

        // Convert a position to the ship coordinates
        public Vector3D PosToShip(Vector3D vec)
        {
            Vector3D bodyPosition = Vector3D.TransformNormal(vec - sc.WorldMatrix.Translation, MatrixD.Transpose(sc.WorldMatrix));
            bodyPosition += sc.Position;
            return bodyPosition;
        }

        public void SetThrust(double d)
        {
            
            if (d > fullthmax) d = fullthmax;   
            
            double COMZ = PosToShip(sc.CenterOfMass).Z;
            List<double> dists = new List<double>();
            foreach (var x in thrusters) dists.Add(PosToShip(x.GetPosition()).Z - COMZ);
            List<int> front = new List<int>();
            List<int> back = new List<int>();
            double frontLever = 0;
            double backLever = 0;
            for (int i = 0; i < dists.Count(); ++i) {
                if (dists[i] < 0)
                {
                    front.Add(i);
                    frontLever += dists[i];
                }
                else
                {
                    back.Add(i);
                    backLever += dists[i];
                }
            }
            double backTh = d / (back.Count() * 1.0 - 1.0 * front.Count() * backLever / frontLever);
            double frontTh = -backLever / frontLever * backTh;
            backT = backTh;
            frontT = frontTh;
            double m = Math.Max(backTh, frontTh);
            if(m > thmax)
            {
                double coeff = thmax / m;
                backTh *= coeff;
                frontTh *= coeff;
            }
            distback = backLever;
            distfront = frontLever;
            foreach (int x in front) thrusters[x].ThrustOverride = (float)frontTh;
            foreach (int x in back) thrusters[x].ThrustOverride = (float)backTh;

        }
        public void ClearThrust()
        {
            foreach (var x in thrusters) x.ThrustOverride = 0;
        }

        public void Save(){}
        
        public void SetRotSpd(Vector3D rotspd)
        {
            g.Roll = -(float)rotspd.X;
            g.Pitch = -(float)rotspd.Y;
            g.Yaw = -(float)rotspd.Z;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) != 0)
            {
                if (argument == "HDamp") hDamp = !hDamp;
                if (argument == "ManualAlt")
                {
                    manualAlt = !manualAlt;
                    targAltitude = GetAltitude();
                    targSpd = 0;
                }
                if (argument == "VDamp") vDamp = !vDamp;
                if (argument == "Spd+") SetSpd(curSpeed + 1);
                if (argument == "Spd-") SetSpd(curSpeed - 1);
            }
            bool trueHDamp = hDamp;
            Vector3D pos = sc.CenterOfMass;
            Vector3D spd = sc.GetShipVelocities().LinearVelocity;
            Vector3D grav = Vector3D.Normalize(sc.GetTotalGravity());
            double gravNorm = sc.GetTotalGravity().Length();
            Vector3D gravin = Vector3D.TransformNormal(grav, MatrixD.Transpose(sc.WorldMatrix));
            Vector3D spdin = Vector3D.TransformNormal(spd, MatrixD.Transpose(sc.WorldMatrix));
            Vector3D MoveIndic = sc.MoveIndicator;
            Vector3D COM = PosToShip(sc.CenterOfMass);

            if (gravin.Y > 0) hDamp = false;



            //orientation
            if (MoveIndic.X != 0 || MoveIndic.Z != 0) trueHDamp = false;
            double inclCommand = rotAngles[curSpeed];
            double angleToRotSpd = 1.0;

            // new orientation
            lastHeading -= grav * grav.Dot(lastHeading);
            lastHeading.Normalize();
            Vector3D right = grav.Cross(lastHeading);
            right.Normalize();

            lastHeading += 0.001 * right * sc.RotationIndicator.Y;
            lastHeading.Normalize();

            Vector3D ntarget = grav;
            double targetX = 0.0, targetZ = 0.0;
            if (trueHDamp)
            {
                targetZ = Clamp(-(spdin.Z) * 0.05, -0.5, 0.5);
                targetX = Clamp(-(spdin.X) * 0.05, -0.5, 0.5);
            }
            else
            {
                targetX = inclCommand * MoveIndic.X;
                targetZ = inclCommand * MoveIndic.Z;
            }
            ntarget -= targetX * right;
            ntarget += targetZ * lastHeading;

            ntarget.Normalize();

            Vector3D headtarget = right.Cross(ntarget);
            headtarget.Normalize();

            Vector3D ntargetin = Vector3D.TransformNormal(ntarget, MatrixD.Transpose(sc.WorldMatrix));
            Vector3D headtargetin = Vector3D.TransformNormal(headtarget, MatrixD.Transpose(sc.WorldMatrix));
            QuaternionD targq = QuaternionD.CreateFromForwardUp(headtargetin, -ntargetin);
            Vector3D axis;
            double angle;
            targq.GetAxisAngle(out axis, out angle);
            axis *= angle;
            axis *= angleToRotSpd;
            SetRotSpd(axis);





            // new altitude
            double curAltitude = GetAltitude();
            double curVSpeed = (curAltitude - lastAltitude) * 60;
            double targAcc = 0;
            if (vDamp) targAcc = Clamp(-targSpd, -accelerations[curSpeed], accelerations[curSpeed]);
            if (MoveIndic.Y != 0) targAcc = MoveIndic.Y * accelerations[curSpeed];
            targSpd += targAcc / 60;
            targSpd = Clamp(targSpd, curVSpeed - maxSpdDrift, curVSpeed + maxSpdDrift);
            targAltitude += targSpd / 60;
            targAltitude = Clamp(targAltitude, curAltitude - maxAltDrift, curAltitude + maxAltDrift);
            double compTargSpd = targSpd + (targAltitude - curAltitude);
            double compTargAcc = targAcc + gravNorm / (-gravin.Y) + (compTargSpd - curVSpeed);

            if (manualAlt) { ClearThrust(); }
            else { SetThrust(compTargAcc * mass); }
            if (tick >= 10)
            {
                tick = 0;
                /*lcd.WriteText($@"grav X={gravin.X:F3} Y={gravin.Y:F3} Z={gravin.Z:F3}
Spd X={spdin.X:F3} Y={spdin.Y:F3} Z={spdin.Z:F3}
AA X={axis.X:F3} Y={axis.Y:F3} Z={axis.Z:F3}
NT {ntargetin} {headtargetin}
hDamp {hDamp} vDamp {vDamp} ManualAlt {manualAlt}
Spd: {speedNames[curSpeed]}
Alt cur {curAltitude:F3} targ {targAltitude:F3}
Spd cur {curVSpeed:F10} targ {targSpd:F10} ctarg {compTargSpd:F10}
Acc targ {targAcc:F10} ctarg {compTargAcc:F10}
pos {sc.GetPosition()} scal {Vector3D.Dot(grav, sc.GetPosition())}
cam X {COM.X:F3} Y {COM.Y:F3} Z {COM.Z:F3}
th back {backT:F3} front {frontT:F3}
dist back {distback:F3} front{distfront:F3}
");*/

                lcd.WriteText($@"Helico control software
Horizontal Dampeners (4) {OnOff(hDamp)}
Vertical Dampeners (5) {OnOff(vDamp)}
Manual Altitude (6) {OnOff(manualAlt)}
Current Altitude {curAltitude:F2}m
Vertical Speed {targSpd:F2}m/s
Speed mode (^8/v9) {speedNames[curSpeed]}
");
            }
            else tick++;

            

            lastAltitude = curAltitude;
        }
    }
}
/*
 * Angle X={angleX:F3} Z={angleZ:F3}
Targets X={targetX:F3} Z={targetZ:F3}*/