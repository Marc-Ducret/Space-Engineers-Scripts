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
        private readonly IMyMotorStator    rotorBase;
        private readonly IMyMotorStator    hingeBase;
        private readonly IMyMotorStator    rotorMiddle;
        private readonly IMyMotorStator    hingeEnd;
        private readonly IMyShipController controller;

        private readonly IMyTextSurface debugPanel;
        private readonly IMyTextSurface controllerPanel;

        private readonly double armLength;

        private readonly double hingeBaseSign;
        private readonly double rotorMiddleSign;
        private readonly double rotorMiddleOffset;

        private Vector3D targetPosition;

        private const double VELOCITY_FACTOR       = 10; // in rad/s/ras : rad/s per radian of error
        private const double TARGET_SPEED          = 0.02;
        private const float  HINGE_END_SENSITIVITY = 0.1f;

        private bool locked;

        struct PID {
            private double p, i, d;
            private double lastError;
            private double intError;

            public string information;

            public void Init(double nP, double nI, double nD) {
                p         = nP;
                i         = nI;
                d         = nD;
                lastError = double.NaN;
                intError  = 0.0;
            }

            public double Eval(double error, double deltaTime) {
                double res = 0;
                res += p  * error;
                if (error * lastError < 0) intError = 0;
                res += i  * intError * deltaTime;
                double derivativeError            = (error - lastError) / deltaTime;
                if (!double.IsNaN(lastError)) res += derivativeError * d;
                lastError =  error;
                intError  += error;

                information = $"e={F(error)} de/dt={F(derivativeError)} S(e dt)={F(intError)}";

                return res;
            }
        }

        private PID pidRotor;
        private PID pidBase;
        private PID pidMid;

        private string blockPrefix;

        private T GetBlock<T>(string name) {
            if (blockPrefix == null) {
                int dotIndex = Me.CustomName.IndexOf('.');
                if (dotIndex <= 0)
                    throw new Exception($"Name `{Me.CustomName}` does not have format `<prefix>.<name>`.");
                blockPrefix = Me.CustomName.Substring(0, dotIndex);
            }

            string           nameWithPrefix = $"{blockPrefix}.{name}";
            IMyTerminalBlock block          = GridTerminalSystem.GetBlockWithName(nameWithPrefix);
            if (block == null) throw new Exception($"Cannot find block `{nameWithPrefix}`");
            if (block is T) return (T) block;
            throw new Exception($"`{nameWithPrefix}` is not of type {typeof(T)}.");
        }

        private double AngleTowards(IMyMotorStator stator, IMyTerminalBlock pointer, Vector3D direction) {
            Vector3D restDir = Vector3D.Normalize(pointer.GetPosition() - stator.GetPosition());
            return Math.Atan2(stator.WorldMatrix.Right.Dot(direction), stator.WorldMatrix.Forward.Dot(direction)) -
                   Math.Atan2(stator.Top.WorldMatrix.Right.Dot(restDir), stator.Top.WorldMatrix.Forward.Dot(restDir));
        }


        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            rotorBase               = GetBlock<IMyMotorStator>("RotorBase");
            hingeBase               = GetBlock<IMyMotorStator>("HingeBase");
            rotorMiddle             = GetBlock<IMyMotorStator>("RotorMiddle");
            hingeEnd                = GetBlock<IMyMotorStator>("HingeEnd");
            IMyCockpit cockpit = GetBlock<IMyCockpit>("Controller");
            controller      = cockpit;
            controllerPanel = cockpit.GetSurface(0);
            debugPanel      = GetBlock<IMyTextSurface>("DebugPanel");

            targetPosition = hingeEnd.GetPosition() - hingeBase.GetPosition();

            if (Math.Abs(Vector3D.Distance(hingeBase.GetPosition(),   rotorMiddle.GetPosition()) -
                         Vector3D.Distance(rotorMiddle.GetPosition(), hingeEnd.GetPosition())) > 0.1)
                throw new Exception("Arms have different lengths.");

            armLength = Vector3D.Distance(hingeBase.GetPosition(), rotorMiddle.GetPosition());

            hingeBaseSign = -Math.Sign(Vector3D.Dot(targetPosition, hingeBase.WorldMatrix.Forward));
            rotorMiddleSign = -Math.Sign(Vector3D.Dot(hingeBase.WorldMatrix.Right, rotorMiddle.WorldMatrix.Up)) *
                              hingeBaseSign;
            Vector3D rotorMiddleToBase = hingeBase.GetPosition() - rotorMiddle.GetPosition();
            rotorMiddleOffset = AngleTowards(rotorMiddle, hingeEnd, Vector3D.Normalize(rotorMiddleToBase));

            pidRotor.Init(1, 0.5, 0.01);
            pidBase.Init(1, 0.5, 0.01);
            pidMid.Init(1, 0.5, 0.01);

            locked = true;
        }

        public void Save() { }

        private void LockStator(IMyMotorStator stator) {
            stator.TargetVelocityRad = 0;
            stator.RotorLock         = true;
        }

        private void UnlockStator(IMyMotorStator stator) {
            stator.TargetVelocityRad = 0;
            stator.RotorLock         = false;
        }

        private void Lock() {
            LockStator(rotorBase);
            LockStator(hingeBase);
            LockStator(rotorMiddle);
            LockStator(hingeEnd);
        }

        private void Unlock() {
            UnlockStator(rotorBase);
            UnlockStator(hingeBase);
            UnlockStator(rotorMiddle);
            UnlockStator(hingeEnd);
        }

        private static string F(double x) => $"{x:+0.00;-0.00}";
        private static string A(double x) => F(Math.IEEERemainder(x, 2 * Math.PI));

        public void Main(string argument, UpdateType updateSource) {
            if ((updateSource & UpdateType.Trigger) != 0) {
                if (argument == "Lock") {
                    if (locked) {
                        locked = false;
                        Unlock();
                    } else {
                        locked = true;
                        Lock();
                    }
                }

                if (argument == "Reset") targetPosition = hingeEnd.GetPosition() - hingeBase.GetPosition();
            }

            Vector3D baseX = rotorBase.WorldMatrix.Forward;
            Vector3D baseY = rotorBase.WorldMatrix.Left;
            Vector3D baseZ = rotorBase.WorldMatrix.Up;

            Func<Vector3D, string> P = value =>
                $"X={F(baseX.Dot(value))} Y={F(baseY.Dot(value))} Z={F(baseZ.Dot(value))}";

            Vector3D curPos = hingeEnd.GetPosition() - hingeBase.GetPosition();

            targetPosition = Vector3D.ClampToSphere(targetPosition, 1.95 * armLength);
            // TODO enforce out of central cylinder
            double       distance  = targetPosition.Length();
            Vector3D     direction = Vector3D.Normalize(targetPosition);
            const double epsilon   = Math.PI / 360;

            double armsAngle = Clamp(2 * Math.Asin(distance / armLength / 2), epsilon, Math.PI - epsilon);
            double pitch     = (Math.PI - armsAngle) / 2 - Angle(direction, rotorBase.WorldMatrix.Up);

            double rotorBaseAngle   = AngleTowards(rotorBase, hingeEnd, direction);
            double hingeBaseAngle   = Clamp(hingeBaseSign * pitch, -Math.PI / 2 + epsilon, +Math.PI / 2 - epsilon);
            double rotorMiddleAngle = rotorMiddleSign * armsAngle + rotorMiddleOffset;

            if (!locked) {
                StatorControl(rotorBase,   rotorBaseAngle,   ref pidRotor);
                StatorControl(hingeBase,   hingeBaseAngle,   ref pidBase);
                StatorControl(rotorMiddle, rotorMiddleAngle, ref pidMid);

                hingeEnd.TargetVelocityRad = HINGE_END_SENSITIVITY * controller.RotationIndicator.X;
            }

            Vector3D deltaPosition = targetPosition - curPos;
            targetPosition = curPos + Vector3D.ClampToSphere(deltaPosition, 1);
            targetPosition +=
                Vector3D.TransformNormal(Vector3D.ClampToSphere(controller.MoveIndicator, 1), controller.WorldMatrix) *
                TARGET_SPEED;
            Echo($"e={Vector3D.Distance(curPos, targetPosition):F1}");

            string information =
                $@"
trg: {P(targetPosition)}
cur: {P(curPos)}
e={F(Vector3D.Distance(curPos, targetPosition))}
rot: trg={A(rotorBaseAngle)} cur={A(rotorBase.Angle)} trg-spd={F(rotorBase.TargetVelocityRad)} pid=[{pidRotor.information}]
bas: trg={A(hingeBaseAngle)} cur={A(hingeBase.Angle)} trg-spd={F(hingeBase.TargetVelocityRad)} pid=[{pidBase.information}]
mid: trg={A(rotorMiddleAngle)} cur={A(rotorMiddle.Angle)} trg-spd={F(rotorMiddle.TargetVelocityRad)} pid=[{pidMid.information}]
hbs={F(hingeBaseSign)} rms={F(rotorMiddleSign)} rmo={A(rotorMiddleOffset)}
lock={locked}
al={F(armLength)} aa={F(armsAngle)} pitch={F(pitch)}
rb-fwd : {P(rotorBase.WorldMatrix.Forward)}
rb-up  : {P(rotorBase.WorldMatrix.Up)}
rbt-fwd: {P(rotorBase.Top.WorldMatrix.Forward)}
rbt-up : {P(rotorBase.Top.WorldMatrix.Up)}
";
            debugPanel.WriteText(information);
            controllerPanel.WriteText(information);
        }

        private static double Clamp(double value, double min, double max) {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static double Angle(Vector3D lhs, Vector3D rhs) => Math.Acos(Vector3D.Dot(lhs, rhs));

        private void StatorControl(IMyMotorStator stator, double targetAngle, ref PID pid) {
            double deltaAngle = Math.IEEERemainder(targetAngle - stator.Angle, 2 * Math.PI);
            double deltaTime  = Clamp(Runtime.TimeSinceLastRun.TotalSeconds, 0.01, 1);
            stator.TargetVelocityRad = (float) (VELOCITY_FACTOR * pid.Eval(deltaAngle, deltaTime));
        }
    }
}