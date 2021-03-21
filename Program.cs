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

        // 60 ticks per second
        const double TickRate = 60.0;

        // Filter coefficient for acceleration estimator
        //  - Updated once every 10 ticks, so divide tick rate by 10.
        readonly float AccelerationFilterCoeff = (float)OnePoleFilterD.LPFCoeff(1.0, TickRate / 10);

        readonly PIDController.Parameters RotationAquisitonParameters = new PIDController.Parameters()
        {
            Kp = 10,
            Ki = 3,
            Kd = 0.5,
        };

        //
        // Control modules
        //

        readonly WcPbApi api;
        readonly SituationalAwareness situationalAwareness;
        readonly HeadingController headingController;

        readonly IMyShipController remoteControlBlock;

        readonly Dictionary<string,DebugPanel> debugPanels;

        long currentTime;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
            try
            {
                api = new WcPbApi();
                api.Activate(Me);

                debugPanels = new Dictionary<string, DebugPanel>();
                InitDebugPanels();

                DebugPanel debugLog = DebugPanelByName("log");
                debugLog.Title = "Debug Log";

                // The remote control block is the forward reference for this grid. I should probably change this to something else....
                remoteControlBlock = GridTerminalSystem.GetBlockWithName("Remote Control") as IMyRemoteControl;

                // Initialize situational awareness tracker
                situationalAwareness = new SituationalAwareness(api, Me, AccelerationFilterCoeff, DebugPanelByName("sitcon"), DebugPanelByName("track"));

                // Initialize 
                headingController = new HeadingController(GridTerminalSystem, remoteControlBlock, DebugPanelByName("heading"), debugLog);

                currentTime = 0;
            }
            catch (Exception e)
            {
                Echo($"*** Caught exception {e.Message}\n{e.StackTrace}");
            }
        }

        DebugPanel DebugPanelByName(string name)
        {
            if (debugPanels.ContainsKey(name))
                return debugPanels[name];
            else
                return null;
        }

        void InitDebugPanels()
        {
            List<IMyTextPanel> textPanels = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(textPanels);
            
            foreach (var textPanel in textPanels)
            {
                DebugPanel debugPanel = new DebugPanel(textPanel, 26);
                string name = textPanel.CustomData.Trim();
                if (name != "") debugPanels.Add(name, debugPanel);
            }
        }

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

                case "toggleControl":
                    headingController.ToggleControl();
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
            // Initialize time and callbacks.
            currentTime = 0;
            Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10;

            // Start the situational awareness module.
            situationalAwareness.Reset();
            situationalAwareness.UpdateThreatsFromWC(currentTime);

            // Set up targe tracking for experimental purposes
            Echo("Setting target to first threat.");
            var threat = situationalAwareness.CurrentThreats.First();
            situationalAwareness.TrackedThreat = threat.Key;

            headingController.Track = new TargetHeadingTrack(remoteControlBlock, threat.Value);

            // Turn on the heading controller
            headingController.Enable = true;
        }

        void Stop()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
            headingController.Enable = false;
        }

        void Update1()
        {
            MyShipVelocities shipVelocities = remoteControlBlock.GetShipVelocities();

            headingController.Update1(shipVelocities.AngularVelocity);

            // Needs to be last thing to happen in Update1.
            currentTime += 1;
        }

        void Update10()
        {
            situationalAwareness.UpdateThreatsFromWC(currentTime);
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

