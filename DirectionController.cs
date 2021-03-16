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
    partial class Program
    {
        public class DirectionController
        {
            public bool Enable = false;
            public Vector3D Direction = Vector3D.Forward;

            DebugPanel debug;

            List<IMyGyro> gyros = new List<IMyGyro>();
            IMyShipController controlBlock;

            PIDController phase1PID;
            PIDController phase2YawPID;
            PIDController phase2PitchPID;

            Vector2D lastControl = new Vector2D();

            double lastAngularVelocityNorm = 0.0;
            double maxAlpha = 0.5;
            double typicalAlpha = 0.5;

            enum ControlMode { Acquiring, Tracking };
            ControlMode mode = ControlMode.Acquiring;

            public DirectionController(IMyGridTerminalSystem gridTerminalSystem, IMyShipController controllerBlock_, PIDController.Parameters phase1Parameters, PIDController.Parameters phase2Parameters, DebugPanel debug_)
            {
                gridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros);
                controlBlock = controllerBlock_;
                debug = debug_;

                phase1PID = new PIDController(phase1Parameters);
                phase2YawPID = new PIDController(phase2Parameters);
                phase2PitchPID = new PIDController(phase2Parameters);
            }

            Vector2D Phase1ControlUpdate(double angularError, double angularVelocitySquared)
            {
                //double closingAngle = angularVelocitySquared / (2 * maxAlpha);
                double closingAngle = angularVelocitySquared / (maxAlpha + typicalAlpha);
                double phase1Control = -phase1PID.Filter(Math.Max(Math.Abs(angularError - closingAngle) - Math.PI * 0.005, 0));

                Vector2D flatDir = new Vector2D() { X = Direction.X, Y = Direction.Y };
                double yawFrac = flatDir.X / flatDir.Length();
                double pitchFrac = flatDir.Y / flatDir.Length();

                return new Vector2D()
                {
                    X = phase1Control * yawFrac,
                    Y = phase1Control * pitchFrac
                };
            }

            Vector2D Phase2ControlUpdate(Vector3D Direction)
            {
                double yawError = -Math.Asin(Direction.X);
                double pitchError = -Math.Asin(Direction.Y);

                return new Vector2D()
                {
                    X = phase2YawPID.Filter(yawError),
                    Y = phase2PitchPID.Filter(pitchError)
                };
            }

            public void Update1()
            {
                debug.Reset();
                if (!Enable)
                {
                    debug.WriteLine("Disabled.");
                    return;
                }

                Vector3D angularVelocity = controlBlock.GetShipVelocities().AngularVelocity;
                double angularVelocityNorm = angularVelocity.Length();

                // Adaptive control logic
                double observedAlpha = Math.Abs(angularVelocityNorm - lastAngularVelocityNorm) * 60.0;
                typicalAlpha = typicalAlpha * 0.8 + observedAlpha * 0.2;
                if (observedAlpha > maxAlpha) maxAlpha = (observedAlpha + maxAlpha) / 2;
                //maxAlpha = Math.Max(Math.Abs(angularVelocityNorm - lastAngularVelocityNorm) * 60.0, maxAlpha);
                lastAngularVelocityNorm = angularVelocityNorm;

                debug.WriteLine($"Max alpha: {maxAlpha}");

                double angularError = Math.Acos(Direction.Z);
                if (angularError > Math.PI / 2) angularError = Math.PI - angularError;

                Vector2D control = new Vector2D();
                double controlResetSensitivity = 0.0;

                // Determine control mode - we want some hysteresis in the system to improve stability during tracking.
                switch (mode)
                {
                    case ControlMode.Acquiring:
                        if (angularError < (Math.PI * 0.05))
                            mode = ControlMode.Tracking;
                        break;

                    case ControlMode.Tracking:
                        if (angularError > (Math.PI * 0.10))
                            mode = ControlMode.Tracking;
                        break;
                }

                switch (mode) 
                {
                    case ControlMode.Acquiring:
                        debug.WriteLine("Acquiring");
                        phase2YawPID.Reset();
                        phase2PitchPID.Reset();                    
                        control = Phase1ControlUpdate(angularError, angularVelocity.LengthSquared());
                        controlResetSensitivity = 0.025;
                        break;


                    case ControlMode.Tracking:
                        debug.WriteLine("Tracking");
                        phase1PID.Reset();
                        control = Phase2ControlUpdate(Direction);
                        controlResetSensitivity = 0.0001;
                        break;
                }
                debug.WriteLine($"Yaw control   = {control.X}");
                debug.WriteLine($"Pitch control = {control.Y}");                
                debug.WriteLine($"Reset snstvty = {controlResetSensitivity}");

                // We don't want to continuously update gyros since space engineers temporarily slows down angular acceleration each time you fiddle with their settings.
                // Only update gyros when the control signal diverges more than 1% from the previous signal. This isn't a good heuristic. I should create a threshold
                // based on a rolling average of recent swings.
                if ((control - lastControl).LengthSquared() > controlResetSensitivity)
                {
                    debug.WriteLine("Updating gyros.");
                    SetYawPitchRate(control.X, control.Y);
                    lastControl = control;
                }
                else
                {
                    debug.WriteLine("Not updating gyros.");
                }
            }

            void SetYawPitchRate(double yawRate, double pitchRate)
            {
                //yawRate = Math.Round(yawRate, 3);
                //pitchRate = Math.Round(pitchRate, 3);
                // Due to orientation of gyros:
                //      Roll is yaw.
                //      Pitch is pitch.
                foreach (var gyro in gyros)
                {
                    gyro.GyroOverride = true;
                    gyro.Roll = (float)yawRate;
                    gyro.Pitch = (float)pitchRate;
                }
            }
        }
    }
}
