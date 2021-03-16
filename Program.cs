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
        //
        // Configuration Parameters
        //

        PIDController.Parameters RotationAquisitonParameters = new PIDController.Parameters()
        {
            Kp = 5,
            Ki = 0,
            Kd = 0
        };
        PIDController.Parameters RotationLockParameters = new PIDController.Parameters()
        {
            Kp = 4,
            Ki = 2,
            Kd = 0.5
        };


        //
        // Control modules
        //

        WcPbApi api;
        SituationalAwareness situationalAwareness;
        DirectionController directionController;
        TargetTracker targetTracker;

        long currentTime;

        public Program()
        {
            try
            {
                api = new WcPbApi();
                api.Activate(Me);

                List<IMyTextPanel> textSurfaces = new List<IMyTextPanel>();
                GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(textSurfaces);

                // The remote control block is the forward reference for this grid. I should probably change this to something else....
                IMyShipController controlBlock = GridTerminalSystem.GetBlockWithName("Remote Control") as IMyRemoteControl;

                // Initialize situational awareness tracker
                situationalAwareness = new SituationalAwareness(api, Me, new DebugPanel(textSurfaces[0], "Situational Awareness", 26).WriteLine);

                // Initialize 
                directionController = new DirectionController(GridTerminalSystem, controlBlock, RotationAquisitonParameters, RotationLockParameters, new DebugPanel(textSurfaces[1], "Direction Controller", 26));
                targetTracker = new TargetTracker(situationalAwareness, directionController, controlBlock, new DebugPanel(textSurfaces[2], "Target Tracker", 26).WriteLine);

                currentTime = 0;
            }
            catch (Exception e)
            {
                Echo($"*** Caught exception {e.Message}\n{e.StackTrace}");
            }
        }

        void DebugEcho(string message) => Echo(message);

        public void Main(string argument, UpdateType updateSource)
        {
            try
            {
                if ((updateSource & UpdateType.Update10) != 0)
                    Update10();

                if ((updateSource & UpdateType.Update1) != 0)
                    Update1();

                if ((updateSource & (UpdateType.Terminal | UpdateType.Trigger)) != 0)
                    RunCommand(argument);
            }
            catch (Exception e)
            {
                Echo($"*** Caught exception {e}");
            }
        }

        void RunCommand(string argument)
        {
            switch (argument)
            {
                case "begin": 
                    Begin();
                    break;

                case "stop":
                    Stop();
                    break;

                case "update1":
                    Update1();
                    break;

                case "update10":
                    Update10();
                    break;
            }
        }

        void Begin()
        {
            currentTime = 0;
            situationalAwareness.Reset();
            situationalAwareness.UpdateThreatsFromWC(currentTime);

            var threat = situationalAwareness.CurrentThreats.First();
            targetTracker.SetTarget(threat.Key);

            Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10;
            targetTracker.Enable = true;
            directionController.Enable = true;
        }

        void Stop()
        {
            targetTracker.Enable = false;
            directionController.Enable = false;
        }

        void Update1()
        {
            targetTracker.Update1();
            directionController.Update1();

            // Needs to be last thing to happen in Update1.
            currentTime += 1;
        }

        void Update10()
        {
            situationalAwareness.UpdateThreatsFromWC(currentTime);
            targetTracker.Update10();
        }
    }

    //public class RotationCalibrator
    //{
    //    public bool Enable;
    //    Action<string> debug;
    //    List<IMyGyro> gyros = new List<IMyGyro>();
    //    IMyShipController controlBlock;

    //    int spinDownTicks = 0;

    //    int rollTicks = 0;
    //    int pitchTicks = 0;
    //    int yawTicks = 0;

    //    enum CalibPhase { SpinUp, SpinDown, Done };
    //    enum CalibAxis { Roll, Pitch, Yaw };

    //    CalibPhase phase;
    //    CalibAxis axis;
        

    //    public RotationCalibrator(IMyGridTerminalSystem gridTerminalSystem, IMyShipController _controlBlock, Action<string> _debug)
    //    {
    //        gridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros);
    //        controlBlock = _controlBlock;
    //        debug = _debug;
    //    }

    //    public void Begin()
    //    {
    //        Enable = true;
    //        phase = CalibPhase.SpinUp;
    //        axis = CalibAxis.Roll;

    //    }

    //    public void Update1()
    //    {
    //        if (!Enable) return;

    //        double angularVelocity = controlBlock.GetShipVelocities().AngularVelocity.Length() / Math.PI;
    //        debug($"Angular velocity = {angularVelocity}");

    //        switch (phase)
    //        {
    //            case CalibPhase.SpinUp:
    //                debug("Spinning up...");
    //                if (angularVelocity > 0.5)
    //                {
    //                    foreach (var gyro in gyros)
    //                        gyro.GyroOverride = false;
    //                    phase = CalibPhase.SpinDown;
    //                    spinDownTicks = 0;
    //                }
    //                else
    //                {
    //                    foreach (var gyro in gyros)
    //                    {
    //                        gyro.GyroOverride = true;
    //                        gyro.Roll = axis == CalibAxis.Roll ? 30.0f : 0.0f;
    //                        gyro.Pitch = axis == CalibAxis.Pitch ? 30.0f : 0.0f;
    //                        gyro.Yaw = axis == CalibAxis.Yaw ? 30.0f : 0.0f;
    //                    }
    //                }
    //                break;

    //            case CalibPhase.SpinDown:
    //                debug("Spinning down...");
    //                spinDownTicks += 1;
    //                if (angularVelocity < 0.01)
    //                {
    //                    switch (axis)
    //                    {
    //                        case CalibAxis.Roll:
    //                            rollTicks = spinDownTicks;
    //                            phase = CalibPhase.SpinUp;
    //                            axis = CalibAxis.Pitch;
    //                            break;

    //                        case CalibAxis.Pitch:
    //                            pitchTicks = spinDownTicks;
    //                            phase = CalibPhase.Done;
    //                            //phase = CalibPhase.SpinUp;
    //                            //axis = CalibAxis.Yaw;
    //                            break;

    //                        case CalibAxis.Yaw:
    //                            yawTicks = spinDownTicks;
    //                            phase = CalibPhase.Done;
    //                            break;
    //                    }
    //                }
    //                else
    //                {
    //                    foreach (var gyro in gyros)
    //                    {
    //                        gyro.GyroOverride = true;
    //                        gyro.Roll = axis == CalibAxis.Roll ? -30.0f : 0.0f;
    //                        gyro.Pitch = axis == CalibAxis.Pitch ? -30.0f : 0.0f;
    //                        gyro.Yaw = axis == CalibAxis.Yaw ? -30.0f : 0.0f;
    //                    }
    //                }
    //                break;

    //            case CalibPhase.Done:
    //                foreach (var gyro in gyros)
    //                {
    //                    gyro.GyroOverride = false;
    //                }

    //                debug($"Roll spindown = {rollTicks}");
    //                debug($"Pitch spindown = {pitchTicks}");
    //                //debug($"Yaw spindown = {yawTicks}");
    //                break;
    //        }
    //    }
    //}

    public class WcPbApi
    {
        private Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> _getSortedThreats;
        private Func<long, int, MyDetectedEntityInfo> _getAiFocus;
        private Func<IMyTerminalBlock, int, MyDetectedEntityInfo> _getWeaponTarget;
        private Action<IMyTerminalBlock, bool, int> _fireWeaponOnce;
        private Func<long, bool> _hasGridAi;
        private Func<IMyTerminalBlock, bool> _hasCoreWeapon;

        public bool Activate(IMyTerminalBlock pbBlock)
        {
            var dict = pbBlock.GetProperty("WcPbAPI")?.As<Dictionary<string, Delegate>>().GetValue(pbBlock);
            if (dict == null) throw new Exception($"WcPbAPI failed to activate");
            return ApiAssign(dict);
        }

        public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
        {
            if (delegates == null)
                return false;
            AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
            AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
            AssignMethod(delegates, "GetWeaponTarget", ref _getWeaponTarget);
            AssignMethod(delegates, "FireWeaponOnce", ref _fireWeaponOnce);
            AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
            AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
            return true;
        }

        private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
        {
            if (delegates == null)
            {
                field = null;
                return;
            }
            Delegate del;
            if (!delegates.TryGetValue(name, out del))
                throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");
            field = del as T;
            if (field == null)
                throw new Exception(
                    $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
        }
        public void GetSortedThreats(IMyTerminalBlock pbBlock, IDictionary<MyDetectedEntityInfo, float> collection) =>
            _getSortedThreats?.Invoke(pbBlock, collection);
        public MyDetectedEntityInfo? GetAiFocus(long shooter, int priority = 0) => _getAiFocus?.Invoke(shooter, priority);
        public MyDetectedEntityInfo? GetWeaponTarget(IMyTerminalBlock weapon, int weaponId = 0) =>
            _getWeaponTarget?.Invoke(weapon, weaponId) ?? null;
        public void FireWeaponOnce(IMyTerminalBlock weapon, bool allWeapons = true, int weaponId = 0) =>
            _fireWeaponOnce?.Invoke(weapon, allWeapons, weaponId);
        public bool HasGridAi(long entity) => _hasGridAi?.Invoke(entity) ?? false;
        public bool HasCoreWeapon(IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;
    }


}

