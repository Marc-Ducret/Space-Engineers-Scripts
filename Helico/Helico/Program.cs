﻿using Sandbox.Game.EntityComponents;
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

        bool dampeners;
        double altitude;
        bool vertDamp;
        IMyGyro g;
        IMyShipController sc;
        IMyTextPanel lcd;
        List<IMyThrust> thrusters;
        double mass;
        double lastAltitude;
        double backT;
        double frontT;
        double distback;
        double distfront;
        bool dock;

        Vector3D lastHeading;

        public double GetAltitude()
        {
            double res = 0.0;
            sc.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out res);
            return res;
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
            dampeners = false;
            altitude = GetAltitude();
            mass = sc.CalculateShipMass().PhysicalMass;
            dock = true;
            lastHeading = sc.WorldMatrix.Forward;
        }

        public Vector3D PosToShip(Vector3D vec)
        {
            Vector3D bodyPosition = Vector3D.TransformNormal(vec - sc.WorldMatrix.Translation, MatrixD.Transpose(sc.WorldMatrix));
            bodyPosition += sc.Position;
            return bodyPosition;
        }

        public void SetThrust(double d, bool enable)
        {
            double fullmax = 250000;
            if (d > fullmax) d = fullmax;   
            double thmax = 90000;
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
                backTh *= thmax;
                frontTh *= thmax;
            }
            distback = backLever;
            distfront = frontLever;
            if (enable)
            {
                foreach (int x in front) thrusters[x].ThrustOverride = (float)frontTh;
                foreach (int x in back) thrusters[x].ThrustOverride = (float)backTh;
            }
        }

        /*public void SetThrust(double d)
        {
            foreach (var x in thrusters) x.ThrustOverride = (float)d/4;
        }*/
        public void ClearThrust()
        {
            foreach (var x in thrusters) x.ThrustOverride = 0;
        }

        public void Save(){}

        double Clamp(double v, double min, double max)
        {
            return Math.Max(min, Math.Min(max, v));
        }

        Vector3D AngleAxis (Vector3D v1, Vector3D v2)
        {
            double angle = Math.Acos(v1.Dot(v2));
            return angle * Vector3D.Normalize(v1.Cross(v2));
        }     
        
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
                if (argument == "Damp") dampeners = !dampeners;
                if (argument == "Dock") dock = !dock;
                if (argument == "Vert") vertDamp = !vertDamp;
                if (argument == "VertTarg") altitude = GetAltitude();
            }
            bool cdampeners = dampeners;
            Vector3D pos = sc.CenterOfMass;
            Vector3D spd = sc.GetShipVelocities().LinearVelocity;
            Vector3D grav = Vector3D.Normalize(sc.GetTotalGravity());
            double gravNorm = sc.GetTotalGravity().Length();
            Vector3D gravin = Vector3D.TransformNormal(grav, MatrixD.Transpose(sc.WorldMatrix));
            Vector3D spdin = Vector3D.TransformNormal(spd, MatrixD.Transpose(sc.WorldMatrix));
            Vector3D MoveIndic = sc.MoveIndicator;
            Vector3D COM = PosToShip(sc.CenterOfMass);


            //orientation
            if (MoveIndic.X != 0 || MoveIndic.Z != 0) cdampeners = false;
            double inclCommand = dock ? 0.05 : 0.5;
            double angleToRotSpd = 1.0;
            /*Vector3D target;
            target.Y = -1;
            if (cdampeners)
            {
                target.Z = Clamp(-(spdin.Z) * 0.05, -0.5, 0.5);
                target.X = Clamp(-(spdin.X) * 0.05, -0.5, 0.5);
            }
            else
            {
                target.X = inclCommand * MoveIndic.X;
                target.Z = inclCommand * MoveIndic.Z;
            }
            target.Normalize();
            Vector3D AA = AngleAxis(target, gravin);
            double yawCommand = 0.02;
            AA.Y -= yawCommand * sc.RotationIndicator.Y;
            AA *= angleToRotSpd;
            if(!newc) SetRotSpd(AA);*/

            // new orientation
            lastHeading -= grav * grav.Dot(lastHeading);
            lastHeading.Normalize();
            Vector3D right = grav.Cross(lastHeading);
            right.Normalize();

            lastHeading += 0.001 * right * sc.RotationIndicator.Y;
            lastHeading.Normalize();

            Vector3D ntarget = grav;
            double targetX = 0.0, targetZ = 0.0;
            if (cdampeners)
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








            //altitude
            double altChangeBys = 5.0; // 3 m/s
            altitude += altChangeBys / 60.0 * MoveIndic.Y;
            mass = sc.CalculateShipMass().PhysicalMass;
            double thThrust = mass * gravNorm / (-gravin.Y);
            double newAltitude = GetAltitude();
            double vertSpeed = (newAltitude - lastAltitude) * 60;//m/s
            
            
            /*double angleX = Math.Atan2(gravin.X, -gravin.Y);
            double angleZ = Math.Atan2(gravin.Z, -gravin.Y);
            if (MoveIndic.X != 0 || MoveIndic.Z != 0) cdampeners = false;
            double targetZ;
            double targetX;
            if (cdampeners){
                targetZ = Clamp(-(spdin.Z) * 0.05, -0.6, 0.6);
                targetX = Clamp(-(spdin.X) * 0.05, -0.6, 0.6);
            }
            else{
                targetX = inclCommand * MoveIndic.X;
                targetZ = inclCommand * MoveIndic.Z;
            }
            g.Roll = (float)(angleZ - targetZ);
            g.Yaw = -(float)(angleX - targetX);
            g.Pitch = (float)(yaw * 0.1);*/

            double targetVSpeed = (altitude - GetAltitude()) / 2 + altChangeBys * MoveIndic.Y;
            double targetThrust = (targetVSpeed - vertSpeed) * 10000;

            //if (vertDamp) SetThrust(thThrust + targetThrust);
            //else ClearThrust();
            SetThrust(thThrust + targetThrust, vertDamp);
            if (!vertDamp) ClearThrust();


            lcd.WriteText($@"grav X={gravin.X:F3} Y={gravin.Y:F3} Z={gravin.Z:F3}
Spd X={spdin.X:F3} Y={spdin.Y:F3} Z={spdin.Z:F3}
AA X={axis.X:F3} Y={axis.Y:F3} Z={axis.Z:F3}
NT {ntargetin} {headtargetin}
damp {dampeners} vd {vertDamp} dock {dock}
Alt cur {GetAltitude():F3} targ {altitude:F3}
pos {sc.GetPosition()} scal {Vector3D.Dot(grav, sc.GetPosition())}
Thrust cur {thrusters[0].CurrentThrust:F3} targ {thThrust + targetThrust:F3} targvspd {targetVSpeed:F3}
mass {mass:F3} gravNorm {gravNorm:F3} theoThrust {thThrust}
cam X {COM.X:F3} Y {COM.Y:F3} Z {COM.Z:F3}
th back {backT:F3} front {frontT:F3}
dist back {distback:F3} front{distfront:F3}
");

            lastAltitude = newAltitude;
        }
    }
}
/*
 * Angle X={angleX:F3} Z={angleZ:F3}
Targets X={targetX:F3} Z={targetZ:F3}*/