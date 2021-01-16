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
        private       IMyMotorStator    hingeMiddle1;
        private       IMyMotorStator    hingeMiddle2;
        private       IMyMotorStator    hingeEnd;
        private       IMyShipController controller;

        private IMyTextSurface debugPanel;

        private double armLength;
        private double middleHingesDistance;

        // TODO how to compute this?
        private double hingeBaseSign    = -1;
        private double hingeMiddle1Sign = -1;
        private double hingeMiddle2Sign = -1;

        private Vector3D targetPosition;

        private const double ANGLE_OFFSET_FULL_VELOCITY = Math.PI / 30;
        private const double FULL_VELOCITY              = Math.PI / 16;

        private const double MAX_ANGLE_OUT_OF_LIMIT         = Math.PI / 90;
        private const double ANGLE_LIMIT_ADJUST_SPEED       = Math.PI / 32;
        private const double ANGLE_LIMIT_ADJUST_SPEED_ANGLE = Math.PI / 32;
        private const double ANGLE_LIMIT_MARGIN             = Math.PI / 90;

        private const double TARGET_SPEED = 0.02;

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            rotorBase = (IMyMotorStator) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.RotorBase");
            hingeBase = (IMyMotorStator) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.HingeBase");
            hingeMiddle1 = (IMyMotorStator) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.HingeMiddle1");
            hingeMiddle2 = (IMyMotorStator) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.HingeMiddle2");
            hingeEnd = (IMyMotorStator) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.HingeEnd");
            controller = (IMyShipController) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.Controller");


            // debugPanel = ((IMyCockpit) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.Controller")).GetSurface(0);
            debugPanel = (IMyTextSurface) GridTerminalSystem.GetBlockWithName($"{BLOCK_PREFIX}.DebugPanel");

            targetPosition = hingeEnd.GetPosition();

            armLength            = Vector3D.Distance(hingeBase.GetPosition(),    hingeMiddle1.GetPosition());
            middleHingesDistance = Vector3D.Distance(hingeMiddle1.GetPosition(), hingeMiddle2.GetPosition());
        }

        public void Save() { }

        public void Main(string argument, UpdateType updateSource) {
            Vector3D baseX = rotorBase.WorldMatrix.Forward;
            Vector3D baseY = rotorBase.WorldMatrix.Right;

            Vector3D baseForward = hingeBase.WorldMatrix.Forward;

            targetPosition = hingeBase.GetPosition() +
                             Vector3D.ClampToSphere(targetPosition - hingeBase.GetPosition(), 1.95 * armLength);
            double       distance  = Vector3D.Distance(hingeBase.GetPosition(), targetPosition);
            Vector3D     direction = Vector3D.Normalize(targetPosition - hingeBase.GetPosition());
            const double epsilon   = Math.PI / 360;
            double middleHingesAngle = Clamp(Math.Acos((distance - middleHingesDistance) / 2 / armLength),
                                             epsilon,
                                             Math.PI / 2 - epsilon);

            double hingeBaseAngle = Clamp(Math.PI / 2 + middleHingesAngle - Angle(direction, baseForward),
                                          -Math.PI / 2                    + epsilon,
                                          +Math.PI / 2                    - epsilon);

            Vector2D directionPlane =
                Vector2D.Normalize(baseX.Dot(direction) * Vector2D.UnitX + baseY.Dot(direction) * Vector2D.UnitY);
            double rotorBaseAngle = Math.Atan2(directionPlane.Y, directionPlane.X);

            StatorControl(rotorBase,    rotorBaseAngle,                       true);
            StatorControl(hingeBase,    hingeBaseAngle,                       true);
            StatorControl(hingeMiddle1, hingeMiddle1Sign * middleHingesAngle, true);
            StatorControl(hingeMiddle2, hingeMiddle2Sign * middleHingesAngle, true);

            Vector3D deltaPosition = targetPosition - hingeEnd.GetPosition();
            targetPosition = hingeEnd.GetPosition() + Vector3D.ClampToSphere(deltaPosition, 1);
            targetPosition +=
                Vector3D.TransformNormal(Vector3D.ClampToSphere(controller.MoveIndicator, 1), controller.WorldMatrix) *
                TARGET_SPEED;
            Echo($"e={Vector3D.Distance(hingeEnd.GetPosition(),                 targetPosition):F1}");
            debugPanel.WriteText($"e={Vector3D.Distance(hingeEnd.GetPosition(), targetPosition):F1}\n");
            debugPanel.WriteText($"m={controller.MoveIndicator}\n",                    true);
            debugPanel.WriteText($"yaw={rotorBase.Angle:F2} -> {rotorBaseAngle:F2}\n", true);
        }

        private static double Clamp(double value, double min, double max) {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static double Angle(Vector3D lhs, Vector3D rhs) => Math.Acos(Vector3D.Dot(lhs, rhs));

        private static double AdjustLimit(double limit, double angle, double targetAngle) {
            double delta = targetAngle - limit;
            double adjust = Clamp(delta, -ANGLE_LIMIT_ADJUST_SPEED_ANGLE, +ANGLE_LIMIT_ADJUST_SPEED_ANGLE) /
                            ANGLE_LIMIT_ADJUST_SPEED_ANGLE * ANGLE_LIMIT_ADJUST_SPEED;
            return limit + adjust;
        }

        private void StatorControl(IMyMotorStator stator, double targetAngle, bool limit) {
            double deltaAngle = Math.IEEERemainder(targetAngle - stator.Angle, 2 * Math.PI);
            double factor = Clamp(deltaAngle, -ANGLE_OFFSET_FULL_VELOCITY, +ANGLE_OFFSET_FULL_VELOCITY) /
                            ANGLE_OFFSET_FULL_VELOCITY;
            Echo($"{stator.CustomName} | {factor:F2} | {deltaAngle:F2}");
            stator.TargetVelocityRad = (float) (factor * FULL_VELOCITY);

            if (limit) {
                stator.LowerLimitRad =
                    (float) Math.Min(AdjustLimit(stator.LowerLimitRad, stator.Angle, targetAngle - ANGLE_LIMIT_MARGIN),
                                     stator.Angle + MAX_ANGLE_OUT_OF_LIMIT);
                stator.UpperLimitRad =
                    (float) Math.Max(AdjustLimit(stator.UpperLimitRad, stator.Angle, targetAngle + ANGLE_LIMIT_MARGIN),
                                     stator.Angle - MAX_ANGLE_OUT_OF_LIMIT);
            }
        }
    }
}