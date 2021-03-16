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
        public class SituationalAwareness
        {
            public class ThreatInfo
            {
                public MyDetectedEntityInfo DetectedInfo;
                public long LastDetected;
                public bool CurrentlyDetected;
            }

            public Dictionary<long, ThreatInfo> CurrentThreats = new Dictionary<long, ThreatInfo>();

            Action<string> debug;
            IMyTerminalBlock me;
            WcPbApi api;
            Dictionary<MyDetectedEntityInfo, float> threatsFromApi = new Dictionary<MyDetectedEntityInfo, float>();

            public SituationalAwareness(WcPbApi api_, IMyTerminalBlock me_, Action<string> debug_)
            {
                api = api_;
                me = me_;
                debug = debug_;
            }

            public void UpdateThreatsFromWC(long currentTime)
            {
                foreach (var threatInfo in CurrentThreats)
                {
                    threatInfo.Value.CurrentlyDetected = false;
                }

                threatsFromApi.Clear();
                api.GetSortedThreats(me, threatsFromApi);

                foreach (var threat in threatsFromApi.Keys)
                {
                    debug($"- {threat.Name} at {threat.HitPosition.Value - me.CubeGrid.WorldMatrix.Translation}");

                    if (CurrentThreats.ContainsKey(threat.EntityId))
                    {
                        CurrentThreats[threat.EntityId].DetectedInfo = threat;
                        CurrentThreats[threat.EntityId].LastDetected = currentTime;
                        CurrentThreats[threat.EntityId].CurrentlyDetected = true;
                    }
                    else
                    {
                        CurrentThreats.Add(threat.EntityId, new ThreatInfo()
                        {
                            DetectedInfo = threat,
                            LastDetected = currentTime,
                            CurrentlyDetected = true
                        });
                    }
                }
            }

            public void Reset()
            {
                CurrentThreats.Clear();
            }

            public Vector3D? GetLastLocation(long id)
            {
                try
                {
                    return CurrentThreats[id].DetectedInfo.HitPosition;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }
}
