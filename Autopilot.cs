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
            readonly IMyShipController controlBlock;
            readonly SituationalAwareness.ThreatInfo target;

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

        public class WeaponHeadingTrack : IHeadingTrack
        {
            readonly IMyShipController controlBlock;
            readonly SituationalAwareness.ThreatInfo target;
            readonly double weaponSpeed;
            readonly double thresh;
            readonly double t0;
            double tlast;
            readonly int maxIters;

            public WeaponHeadingTrack(IMyShipController controlBlock_, SituationalAwareness.ThreatInfo target_, double weaponSpeed_, double t0_, double thresh_ = 0.01, int maxIters_ = 10)
            {
                controlBlock = controlBlock_;
                target = target_;
                weaponSpeed = weaponSpeed_;
                thresh = thresh_;
                t0 = t0_;
                tlast = t0;
                maxIters = maxIters_;
            }

            double? SolveForT(double t, Vector3D x, Vector3D v, Vector3D a)
            {
                // The motion of an object under acceleration is a quadratic equation: x0 + vt + 0.5at^2.
                // Given a line:  y(t) = st x(t)/||x(t)|| where s is a known initial speed.
                //  x(t) = y(t) => ||x(t)|| = st. Once we know t, x(t)/||x(t)|| is relatively easy to compute.
                // The fastest way to solve ||x(t)||= st is to solve ||x(t)||^2-(st)^2=0 by a Newton iteration. And we can
                // reuse the solution from the previous timestep to accelerate the solution in the next timestpe.
                //
                // let f(t)  = ||x(t)||^2-(st)^2.
                //           = ||x0 + vt + 0.5at||^2 - (st)^2
                //           = <x,x> + 2<v,x> t + (<v,v> + <a,x> - s^2) t^2 + <a,v> t^3 + 0.25 <a,a> t^4
                //     f'(t) = 2<v,x> + 2(<v,v> + <a,x> - s^2) t + 3<a,v> t^2 + <a,a> t^3
                //

                for (int i = 0; i < maxIters; i++)
                {
                    double a0 = x.LengthSquared();
                    double a1 = 2 * x.Dot(v);
                    double a2 = v.LengthSquared() + x.Dot(a) - weaponSpeed * weaponSpeed;
                    double a3 = v.Dot(a);
                    double a4 = 0.25 * a.LengthSquared();

                    double f_t = a0 + t * (a1 + t * (a2 + t * (a3 + t * a4)));
                    if (Math.Abs(f_t) < thresh) return t;
                        
                    double df_t = a1 + t * (2 * a2 + t * (3 * a3 + t * 4 * a4));
                    t -= -f_t / df_t;
                }

                // Oops, didn't converge. :(
                return null;
            }

            public Vector3D Forward {
                get {

                    Vector3D direction    = Vector3D.TransformNormal(controlBlock.WorldMatrix.Translation - target.DetectedInfo.Position, MatrixD.Transpose(controlBlock.WorldMatrix));
                    Vector3D velocity     = Vector3D.TransformNormal(controlBlock.GetShipVelocities().LinearVelocity - target.DetectedInfo.Velocity, MatrixD.Transpose(controlBlock.WorldMatrix));
                    Vector3D acceleration = Vector3D.TransformNormal(target.EstimatedAcceleration, MatrixD.Transpose(controlBlock.WorldMatrix));

                    double? t = SolveForT(tlast, direction, velocity, acceleration);
                    if (!t.HasValue) t = SolveForT(t0, direction, velocity, acceleration);

                    Vector3D pointAt;
                    if (!t.HasValue)
                    {
                        pointAt = direction;
                    }
                    else
                    {
                        tlast = t.Value;
                        pointAt = direction + t.Value * (velocity + 0.5 * t.Value * acceleration);
                    }
                    pointAt.Normalize();
                    return pointAt;
                }
            }

            public Vector3D? Up {
                get {
                    return null;
                }
            }
        }


        public class Autopilot
        {
            const double rotationIntegralBreakaway = 15.0 * Math.PI / 180;
            readonly PIDController.Parameters rotationPIDParams = new PIDController.Parameters()
            {
                Kp = 150.0,
                Ki = 150.0,
                Kd = 0.0,
            };

            readonly PIDController.Parameters thrustPIDParams = new PIDController.Parameters()
            {
                Kp = 5.0,
                Ki = 0.0,
                Kd = 0.0
            };

            public IHeadingTrack Track;

            readonly DebugPanel headingDebug, thrustDebug;
            readonly NavigationSystems navigation;
            
            readonly PIDController rotationPID;
            readonly PIDController thrustPID;

            Vector3D lastControl;

            Vector3D destination = new Vector3D()
            {
                X = 2809.95429605016,
                Y = 593.830686901902,
                Z = 1967.64576599728
            };

            public Autopilot(NavigationSystems navigation_, DebugPanel headingDebug_, DebugPanel thrustDebug_)
            {
                // Initialize debug panels
                headingDebug = headingDebug_;
                thrustDebug = thrustDebug_;
                if (headingDebug != null) headingDebug.Title = "Heading Control";
                if (thrustDebug != null) thrustDebug.Title = "Thrust Control";

                navigation = navigation_;

                thrustPID = new PIDController(thrustPIDParams);
                rotationPID = new PIDController(rotationPIDParams);
            }

            public void Update1()
            {
                MatrixD orientation = navigation.Orientation;
                MyShipVelocities shipVelocities = navigation.Velocities;

                UpdateGyroControl(orientation, shipVelocities.AngularVelocity);
                UpdateThrustControl(orientation, shipVelocities.LinearVelocity);
            }

            public void UpdateGyroControl(MatrixD orientation, Vector3D angularVelocityWorld)
            {
                // X - pitch
                // Y - yaw
                // Z - roll

                headingDebug?.Reset();
                if (Track == null)
                {
                    headingDebug?.WriteLine("No track.");
                    return;
                }

                Vector3D trackDirection = Track.Forward;
                headingDebug?.WriteLine($"d = {Math.Round(trackDirection.X, 2)}, {Math.Round(trackDirection.Y, 2)}, {Math.Round(trackDirection.Z, 2)}");

                Vector3D angularVelocity = Vector3D.TransformNormal(angularVelocityWorld, MatrixD.Transpose(orientation));
                headingDebug?.WriteLine($"w = {Math.Round(angularVelocity.X, 2)}, {Math.Round(angularVelocity.Y, 2)}, {Math.Round(angularVelocity.Z, 2)}");

                Vector3D angularVelocityVectorDirTarget = Vector3D.Cross(Vector3D.Forward, trackDirection);
                headingDebug?.WriteLine($"wt = {Math.Round(angularVelocityVectorDirTarget.X, 2)}, {Math.Round(angularVelocityVectorDirTarget.Y, 2)}, {Math.Round(angularVelocityVectorDirTarget.Z, 2)}");

                double rotationError = Math.Acos(trackDirection.Z);

                const double maxAlpha = .5;
                double approachOmega = -Vector3D.Dot(angularVelocity, angularVelocityVectorDirTarget);
                headingDebug?.WriteLine($"w_app = {Math.Round(approachOmega, 3)}");
                double stoppingTheta = approachOmega / (2.0 * maxAlpha);
                double rotationErrorStopping = rotationError - stoppingTheta;

                headingDebug?.WriteLine($"e = {Math.Round(rotationError * 180.0 / Math.PI,2)}, ts = {Math.Round(stoppingTheta * 180.0 / Math.PI, 2)}, es = {Math.Round(rotationErrorStopping * 180.0 / Math.PI, 2)}");

                // We have to force the angular velocity vector to always be in the upper half-plane, so that the rotationError signal
                // is bidirectional. If the rotationError signal isn't bidirectional then we can't unwind the integral component of
                // the PID controller and tracking breaks.
                if (angularVelocityVectorDirTarget.Y < 0)
                {
                    angularVelocityVectorDirTarget = -angularVelocityVectorDirTarget;
                    rotationErrorStopping = -rotationErrorStopping;
                }

                bool useIntegral = rotationError < rotationIntegralBreakaway;
                double angularVelocityVectorMagnitudeTarget = rotationPID.Filter(rotationErrorStopping, !useIntegral);

                headingDebug?.WriteLine($"pid_i = {rotationPID.IntegralState}");

                Vector3D angularVelocityTarget = Vector3D.ClampToSphere(-angularVelocityVectorMagnitudeTarget * angularVelocityVectorDirTarget, Math.PI);                
                Vector3D angularVelocityError = angularVelocityTarget - angularVelocity;


                // I'm not adding any extra control signal to angular velocity. I've experimented to try eke out a little more torque by overcompensating but the increased settling time
                // doesn't make it worthwhile. Maybe on a different drone design.
                Vector3D angularControl = angularVelocityTarget;

                headingDebug?.WriteLine($"control = {Math.Round(angularControl.X, 3)}, {Math.Round(angularControl.Y, 3)}");
                angularControl = Vector3D.ClampToSphere(angularControl, Math.PI);

                ApplyGyroControlUpdate(angularControl, 0.0);
            }

            public void UpdateThrustControl(MatrixD orientation, Vector3D linearVelocityWorld)
            {
                Vector3D velocity = Vector3D.TransformNormal(linearVelocityWorld, MatrixD.Transpose(orientation));
                Vector3D destinationRelative = Vector3D.TransformNormal(destination - orientation.Translation, MatrixD.Transpose(orientation));

                float mass = navigation.Mass;

                Vector3D availableBrakingAcceleration = navigation.MaxThrustInDirection(-velocity) / mass;
                Vector3D predictedStopPoint = -0.5 * velocity * velocity / availableBrakingAcceleration;

                Vector3D positionError = destinationRelative - predictedStopPoint;

                Vector3D desiredVelocity = Vector3D.ClampToSphere(thrustPID.Filter(positionError), SpeedCap);
                Vector3D velocityError = velocity - desiredVelocity;
                Vector3D desiredThrustPercentage = Vector3D.Clamp(0.5 * velocityError, -Vector3D.One, Vector3D.One);

                thrustDebug?.Reset();
                thrustDebug?.WriteLine($"x     = {DebugPanel.ToString(destinationRelative)}");
                thrustDebug?.WriteLine($"v     = {DebugPanel.ToString(velocity)}");
                thrustDebug?.WriteLine($"as    = {DebugPanel.ToString(availableBrakingAcceleration)}");
                thrustDebug?.WriteLine($"xs    = {DebugPanel.ToString(predictedStopPoint)}");
                thrustDebug?.WriteLine($"vt    = {DebugPanel.ToString(desiredVelocity)}");
                thrustDebug?.WriteLine($"v_err = {DebugPanel.ToString(velocityError)}");
                thrustDebug?.WriteLine($"t     = {DebugPanel.ToString(desiredThrustPercentage)}");

                navigation.SetThrustPercentage(desiredThrustPercentage);
            }

            void ApplyGyroControlUpdate(Vector3D controlSignal, double sensitivity)
            {
                // We don't want to continuously update gyros since space engineers temporarily slows down angular acceleration each time you fiddle with their settings.
                // Only update gyros when the control signal diverges more than 1% from the previous signal. This isn't a good heuristic. I should create a threshold
                // based on a rolling average of recent swings.
                if ((controlSignal - lastControl).LengthSquared() > sensitivity)
                {
                    headingDebug?.WriteLine("Updating gyros.");
                    navigation.SetRotationRate(controlSignal);
                    lastControl = controlSignal;
                }
                else
                {
                    headingDebug?.WriteLine("Not updating gyros.");
                }
            }
        }
    }
}
