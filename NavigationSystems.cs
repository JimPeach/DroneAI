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
        public class NavigationSystems
        {
            readonly IMyGridTerminalSystem gridTerminalSystem;
            readonly IMyShipController controlBlock;
            readonly Dictionary<Vector3I, int> directionIndex = new Dictionary<Vector3I, int>();

            readonly List<IMyGyro> gyros = new List<IMyGyro>();
            readonly List<IMyThrust> allThrusters = new List<IMyThrust>();
            readonly List<List<IMyThrust>> thrusters = new List<List<IMyThrust>>();
            readonly List<float> maxThrust = new List<float>();

            bool enable = false;

            public NavigationSystems(IMyGridTerminalSystem gridTerminalSystem_, IMyShipController controlBlock_)
            {
                gridTerminalSystem = gridTerminalSystem_;
                controlBlock = controlBlock_;

                directionIndex = GetInitialDirectionIndex();
                for (int i = 0; i < directionIndex.Count; i++)
                {
                    thrusters.Add(new List<IMyThrust>());
                    maxThrust.Add(0f);
                }

                UpdateFromGrid();
            }

            public void UpdateFromGrid()
            {
                UpdateGyrosFromGrid();
                UpdateThrustersFromGrid();
            }

            public bool Enable {
                get { return enable; }
                set {
                    enable = value;
                    controlBlock.DampenersOverride = !value;
                    foreach (var thruster in allThrusters) thruster.ThrustOverride = 0;
                    foreach (var gyro in gyros) gyro.GyroOverride = value;
                }
            }

            public MatrixD Orientation { get { return controlBlock.WorldMatrix; } }
            public MyShipVelocities Velocities { get { return controlBlock.GetShipVelocities(); } }
            public float Mass {  get { return controlBlock.CalculateShipMass().PhysicalMass; } }

            public Vector3D MaxThrustInDirection(Vector3D direction)
            {
                Vector3D directionSign = Vector3D.SignNonZero(direction);
                return new Vector3D()
                {
                    X = directionSign.X * (double)maxThrust[directionSign.X < 0 ? 0 : 1],
                    Y = directionSign.Y * (double)maxThrust[directionSign.Y < 0 ? 2 : 3],
                    Z = directionSign.Z * (double)maxThrust[directionSign.Z < 0 ? 4 : 5]
                };
            }

            public void SetRotationRate(Vector3D rotationRate)
            {
                if (!enable) return;
                // X - pitch
                // Y - yaw
                // Z - roll
                // 
                // BUT due to orientation of gyros:
                //      Roll is yaw.
                //      Pitch is pitch.
                //      Yaw is roll.
                //
                // I should figure that out programmatically per gyro :P

                float yawRate = (float)rotationRate.Y;
                float pitchRate = -(float)rotationRate.X;
                float rollRate = (float)rotationRate.Z;

                foreach (var gyro in gyros)
                {
                    gyro.GyroOverride = true;
                    gyro.Roll = (float)yawRate;
                    gyro.Pitch = (float)pitchRate;
                    gyro.Yaw = (float)rollRate;
                }
            }

            public void SetThrustPercentage(Vector3D thrustPercentage)
            {
                if (!enable) return;

                if (thrustPercentage.X > 0)
                    SetThrustPercentageOnThrusters(thrusters[0], thrusters[1], thrustPercentage.X);
                else
                    SetThrustPercentageOnThrusters(thrusters[1], thrusters[0], -thrustPercentage.X);

                if (thrustPercentage.Y > 0)
                    SetThrustPercentageOnThrusters(thrusters[2], thrusters[3], thrustPercentage.Y);
                else
                    SetThrustPercentageOnThrusters(thrusters[3], thrusters[2], -thrustPercentage.Y);

                if (thrustPercentage.Z > 0)
                    SetThrustPercentageOnThrusters(thrusters[4], thrusters[5], thrustPercentage.Z);
                else
                    SetThrustPercentageOnThrusters(thrusters[5], thrusters[4], -thrustPercentage.Z);
            }

            void SetThrustPercentageOnThrusters(List<IMyThrust> thrustersToSet, List<IMyThrust> thrustersToStop, double percentage)
            {
                foreach (IMyThrust thruster in thrustersToSet)
                    thruster.ThrustOverridePercentage = (float)percentage;

                foreach (IMyThrust thruster in thrustersToStop)
                    thruster.ThrustOverridePercentage = 0.0f;
            }

            Dictionary<Vector3I, int> GetInitialDirectionIndex()
            {
                return new Dictionary<Vector3I, int>()
                {
                    { new Vector3I() { X = 1 }, 0 },
                    { new Vector3I() { X = -1 }, 1 },
                    { new Vector3I() { Y = 1 }, 2 },
                    { new Vector3I() { Y = -1 }, 3 },
                    { new Vector3I() { Z = 1 }, 4 },
                    { new Vector3I() { Z = -1 }, 5 }
                };
            }

            void UpdateGyrosFromGrid()
            {
                gyros.Clear();
                gridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros);
            }

            void UpdateThrustersFromGrid()
            {
                allThrusters.Clear();
                for (int i = 0; i < directionIndex.Count; i++)
                {
                    thrusters[i].Clear();
                    maxThrust[i] = 0f;
                }

                gridTerminalSystem.GetBlocksOfType<IMyThrust>(allThrusters);
                foreach (IMyThrust thruster in allThrusters)
                {
                    Vector3I direction = thruster.GridThrustDirection;
                    thrusters[directionIndex[direction]].Add(thruster);
                    maxThrust[directionIndex[direction]] += thruster.MaxEffectiveThrust;
                }
            }
        }
    }
}
