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

namespace IngameScript {
    partial class Program : MyGridProgram {
        private const string            BLOCK_PREFIX = "TestArm";
        private       IMyMotorStator    rotorBase;
        private       IMyMotorStator    hingeBase;
        private       IMyMotorStator    rotorMiddle;
        private       IMyMotorStator    hingeEnd;
        private       IMyShipController controller;

        private IMyTextSurface debugPanel;

        private double armLength;
        private double armLength2;
        private double middleHingesDistance;

        // TODO how to compute this?
        private double hingeBaseSign    = -1;

        private Vector3D targetPosition;

        private const double ANGLE_OFFSET_FULL_VELOCITY = Math.PI / 30;
        private const double VELOCITY_FACTOR              = 10; // in rad/s/ras : rad/s per radian of error

        private const double MAX_ANGLE_OUT_OF_LIMIT         = Math.PI / 90;
        private const double ANGLE_LIMIT_ADJUST_SPEED       = Math.PI / 32;
        private const double ANGLE_LIMIT_ADJUST_SPEED_ANGLE = Math.PI / 32;
        private const double ANGLE_LIMIT_MARGIN             = Math.PI / 90;

        private const double TARGET_SPEED = 0.02;

        bool lockr;

        struct PID
        {
            double P, I, D;
            public double lastError;
            public double IntError;

            public void Init(double nP, double nI, double nD)
            {
                P = nP;
                I = nI;
                D = nD;
                lastError = double.NaN;
                IntError = 0.0;
            }

            public double eval(double error)
            {
                double res = 0;
                res += P * error;
                if (error * lastError < 0) IntError = 0;
                res += I * IntError;
                if (!double.IsNaN(lastError)) res += (error - lastError) * D;
                lastError = error;
                IntError += error;
                
                return res;
            }
        }

        PID PIDrotor;
        PID PIDbase;
        PID PIDmid;

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            rotorBase = (IMyMotorStator) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.RotorBase");
            hingeBase = (IMyMotorStator) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.HingeBase");
            rotorMiddle = (IMyMotorStator) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.RotorMiddle");
            hingeEnd = (IMyMotorStator) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.HingeEnd");
            controller = (IMyShipController) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.Controller");


            // debugPanel = ((IMyCockpit) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.Controller")).GetSurface(0);
            debugPanel = (IMyTextSurface) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.DebugPanel");

            targetPosition = hingeEnd.GetPosition() - hingeBase.GetPosition();

            armLength = Vector3D.Distance(hingeBase.GetPosition(), rotorMiddle.GetPosition());
            armLength2 = Vector3D.Distance(hingeEnd.GetPosition(), rotorMiddle.GetPosition());

            PIDrotor.Init(1, 0, 0);
            PIDbase.Init(1, 0, 0);
            PIDmid.Init(1, 0, 0);

            lockr = true;
        }

        public void Save() {
        }

        public void LockStator(IMyMotorStator stator)
        {
            stator.TargetVelocityRad = 0;
            stator.RotorLock = true;
        }
        public void UnlockStator(IMyMotorStator stator)
        {
            stator.TargetVelocityRad = 0;
            stator.RotorLock = false;
        }

        public void Lock()
        {
            LockStator(rotorBase);
            LockStator(hingeBase);
            LockStator(rotorMiddle);
        }

        public void Unlock()
        {
            UnlockStator(rotorBase);
            UnlockStator(hingeBase);
            UnlockStator(rotorMiddle);
        }

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & UpdateType.Trigger) != 0) {
                if (argument == "Lock") {
                    if (lockr) {lockr = false; Unlock();}
                    else {lockr = true; Lock();}
                }
                if (argument == "Reset") targetPosition = hingeEnd.GetPosition() - hingeBase.GetPosition();
            }
            Vector3D baseX = rotorBase.WorldMatrix.Forward;
            Vector3D baseY = rotorBase.WorldMatrix.Right;
            Vector3D baseZ = rotorBase.WorldMatrix.Down;

            Vector3D baseForward = hingeBase.WorldMatrix.Forward;
            Vector3D curPos = hingeEnd.GetPosition() - hingeBase.GetPosition();

            targetPosition = Vector3D.ClampToSphere(targetPosition, 1.95 * armLength);
            double       distance  = targetPosition.Length();
            Vector3D     direction = Vector3D.Normalize(targetPosition);
            const double epsilon   = Math.PI / 360;
            double midAngle = Clamp(2*Math.Acos(distance / 2 / armLength), epsilon, Math.PI - epsilon);

            double directionAngleHeight = Math.PI / 2 - Angle(direction, rotorBase.WorldMatrix.Up);

            double hingeBaseAngle = Clamp(Math.PI / 2 - midAngle / 2 - directionAngleHeight,
                                          -Math.PI / 2                    + epsilon,
                                          +Math.PI / 2                    - epsilon);

            Vector2D directionPlane =
                Vector2D.Normalize(baseX.Dot(direction) * Vector2D.UnitX + baseY.Dot(direction) * Vector2D.UnitY);
            double rotorBaseAngle = Math.Atan2(directionPlane.Y, directionPlane.X);
            if (!lockr)
            {
                StatorControl(rotorBase, rotorBaseAngle, ref PIDrotor);
                StatorControl(hingeBase, hingeBaseAngle, ref PIDbase);
                StatorControl(rotorMiddle, -midAngle, ref PIDmid);
            }

            Vector3D deltaPosition = targetPosition - curPos;
            targetPosition = curPos + Vector3D.ClampToSphere(deltaPosition, 1);
            targetPosition +=
                Vector3D.TransformNormal(Vector3D.ClampToSphere(controller.MoveIndicator, 1), controller.WorldMatrix) *
                TARGET_SPEED;
            Echo($"e={Vector3D.Distance(curPos,                 targetPosition):F1}");
            debugPanel.WriteText($@"target X={baseX.Dot(targetPosition):F3} Y={baseY.Dot(targetPosition):F3} Z={baseZ.Dot(targetPosition):F3}
cur X={baseX.Dot(curPos):F3} Y={baseY.Dot(curPos):F3} Z={baseZ.Dot(curPos):F3}
hbf {baseForward}
e={Vector3D.Distance(curPos, targetPosition):F3}
rotor target={rotorBaseAngle:F3} cur {rotorBase.Angle:F3} targspd {rotorBase.TargetVelocityRad:F3} ie {PIDrotor.IntError:F3}
base target={hingeBaseAngle:F3} cur {hingeBase.Angle:F3} targspd {hingeBase.TargetVelocityRad:F3} ie {PIDbase.IntError:F3}
mid target={2 * Math.PI - midAngle:F3} cur {rotorMiddle.Angle:F3} targspd {rotorMiddle.TargetVelocityRad:F3}  ie {PIDmid.IntError:F3} le {PIDmid.lastError:F3}
lock {lockr}
al {armLength:F3} al2 {armLength2:F3}
");
        }

        private static double Clamp(double value, double min, double max) {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static double Angle(Vector3D lhs, Vector3D rhs) => Math.Acos(Vector3D.Dot(lhs, rhs));

        private void StatorControl(IMyMotorStator stator, double targetAngle, ref PID pid) {
            double deltaAngle = Math.IEEERemainder(targetAngle - stator.Angle, 2 * Math.PI);

            stator.TargetVelocityRad = (float)(VELOCITY_FACTOR * pid.eval(deltaAngle));

            /*double factor = Clamp(deltaAngle, -ANGLE_OFFSET_FULL_VELOCITY, +ANGLE_OFFSET_FULL_VELOCITY) /
                            ANGLE_OFFSET_FULL_VELOCITY;
            Echo($"{stator.CustomName} | {factor:F2} | {deltaAngle:F2}");
            stator.TargetVelocityRad = (float) (factor * FULL_VELOCITY);*/
        }
    }
}

