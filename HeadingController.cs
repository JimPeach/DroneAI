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
        public interface IHeadingTrack
        {
            Vector3D Forward { get; }
            Vector3D? Up { get; }
        }

        public class TargetHeadingTrack : IHeadingTrack
        {
            IMyShipController controlBlock;
            SituationalAwareness.ThreatInfo target;

            public TargetHeadingTrack(IMyShipController controlBlock_, SituationalAwareness.ThreatInfo target_)
            {
                controlBlock = controlBlock_;
                target = target_;
            }

            public Vector3D Forward {
                get {
                    Vector3D direction = Vector3D.TransformNormal(controlBlock.WorldMatrix.Translation - target.DetectedInfo.Position, MatrixD.Transpose(controlBlock.WorldMatrix));
                    direction.Normalize();
                    return direction;
                }
            }

            public Vector3D? Up {
                get {
                    return null;
                }
            }
        }

        public class HeadingController
        {
            public bool Enable = false;

            bool EnableControl;
            public void ToggleControl()
            {
                EnableControl = !EnableControl;
                foreach (var gyro in gyros)
                {
                    gyro.GyroOverride = EnableControl;
                }
            }

            readonly DebugPanel debug, debugLog;
            readonly List<IMyGyro> gyros = new List<IMyGyro>();
            readonly IMyShipController controlBlock;

            Vector3D lastControl;

            public IHeadingTrack Track;

            const double integralBreakaway = 15.0 * Math.PI / 180;
            readonly PIDController.Parameters parameters = new PIDController.Parameters()
            {
                Kp = 150.0,
                Ki = 150.0,
                Kd = 0.0,
            };

            PIDController rotationPID;

            public HeadingController(IMyGridTerminalSystem gridTerminalSystem, IMyShipController controllerBlock_, DebugPanel debug_, DebugPanel debugLog_)
            {
                gridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros);
                controlBlock = controllerBlock_;
                debug = debug_;
                if (debug != null)
                    debug.Title = "Heading Control";

                debugLog = debugLog_;

                rotationPID = new PIDController(parameters);
            }

            public void Update1(Vector3D angularVelocityWorld)
            {
                // X - pitch
                // Y - yaw
                // Z - roll

                debug?.Reset();
                if (!Enable || Track == null)
                {
                    debug?.WriteLine("Disabled.");
                    return;
                }

                Vector3D trackDirection = Track.Forward;
                debug?.WriteLine($"d = {Math.Round(trackDirection.X, 2)}, {Math.Round(trackDirection.Y, 2)}, {Math.Round(trackDirection.Z, 2)}");

                Vector3D angularVelocity = Vector3D.TransformNormal(angularVelocityWorld, MatrixD.Transpose(controlBlock.WorldMatrix));
                debug?.WriteLine($"w = {Math.Round(angularVelocity.X, 2)}, {Math.Round(angularVelocity.Y, 2)}, {Math.Round(angularVelocity.Z, 2)}");

                Vector3D angularVelocityVectorDirTarget = Vector3D.Cross(Vector3D.Forward, trackDirection);
                debug?.WriteLine($"wt = {Math.Round(angularVelocityVectorDirTarget.X, 2)}, {Math.Round(angularVelocityVectorDirTarget.Y, 2)}, {Math.Round(angularVelocityVectorDirTarget.Z, 2)}");

                double rotationError = Math.Acos(trackDirection.Z);

                const double maxAlpha = .5;
                double approachOmega = -Vector3D.Dot(angularVelocity, angularVelocityVectorDirTarget);
                debug?.WriteLine($"w_app = {Math.Round(approachOmega, 3)}");
                double stoppingTheta = approachOmega / (2.0 * maxAlpha);
                double rotationErrorStopping = rotationError - stoppingTheta;

                debug?.WriteLine($"e = {Math.Round(rotationError * 180.0 / Math.PI,2)}, ts = {Math.Round(stoppingTheta * 180.0 / Math.PI, 2)}, es = {Math.Round(rotationErrorStopping * 180.0 / Math.PI, 2)}");

                // We have to force the angular velocity vector to always be in the upper half-plane, so that the rotationError signal
                // is bidirectional. If the rotationError signal isn't bidirectional then we can't unwind the integral component of
                // the PID controller and tracking breaks.
                if (angularVelocityVectorDirTarget.Y < 0)
                {
                    angularVelocityVectorDirTarget = -angularVelocityVectorDirTarget;
                    rotationErrorStopping = -rotationErrorStopping;
                }

                bool useIntegral = rotationError < integralBreakaway;
                double angularVelocityVectorMagnitudeTarget = rotationPID.Filter(rotationErrorStopping, !useIntegral);

                debug?.WriteLine($"pid_i = {rotationPID.IntegralState}");

                Vector3D angularVelocityTarget = Vector3D.ClampToSphere(-angularVelocityVectorMagnitudeTarget * angularVelocityVectorDirTarget, Math.PI);                
                Vector3D angularVelocityError = angularVelocityTarget - angularVelocity;


                // I'm not adding any extra control signal to angular velocity. I've experimented to try eke out a little more torque by overcompensating but the increased settling time
                // doesn't make it worthwhile. Maybe on a different drone design.
                Vector3D angularControl = angularVelocityTarget;

                debug?.WriteLine($"control = {Math.Round(angularControl.X, 3)}, {Math.Round(angularControl.Y, 3)}");
                angularControl = Vector3D.ClampToSphere(angularControl, Math.PI);

                if (EnableControl) ApplyControlUpdate(angularControl, 0.0);
            }

            void ApplyControlUpdate(Vector3D controlSignal, double sensitivity)
            {
                // We don't want to continuously update gyros since space engineers temporarily slows down angular acceleration each time you fiddle with their settings.
                // Only update gyros when the control signal diverges more than 1% from the previous signal. This isn't a good heuristic. I should create a threshold
                // based on a rolling average of recent swings.
                if ((controlSignal - lastControl).LengthSquared() > sensitivity)
                {
                    debug?.WriteLine("Updating gyros.");
                    SetYawPitchRate(controlSignal.Y, -controlSignal.X);
                    lastControl = controlSignal;
                }
                else
                {
                    debug?.WriteLine("Not updating gyros.");
                }
            }

            void SetYawPitchRate(double yawRate, double pitchRate)
            {
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
