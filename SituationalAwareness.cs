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
                public Vector3 EstimatedAcceleration = Vector3D.Zero;
                public long LastObservedTick = -1;
                public bool CurrentlyDetected = false;

                OnePoleFilterV accelerationFilter;

                public ThreatInfo(float accelerationEstimatorFilterCoeff)
                {
                    accelerationFilter = new OnePoleFilterV(accelerationEstimatorFilterCoeff);
                }

                public void Update(MyDetectedEntityInfo info, long thisTick)
                {
                    bool isNan = float.IsNaN(EstimatedAcceleration.X) || float.IsNaN(EstimatedAcceleration.Y) || float.IsNaN(EstimatedAcceleration.Z);
                    if (!isNan && thisTick < LastObservedTick + 20)
                    {
                        Vector3 newEstimatedAcceleration = (info.Velocity - DetectedInfo.Velocity) * (float)TickRate / (float)(thisTick - LastObservedTick);
                        EstimatedAcceleration = accelerationFilter.Filter(newEstimatedAcceleration);
                    }
                    else
                    {
                        accelerationFilter.Reset();
                        EstimatedAcceleration = Vector3.Zero;
                    }

                    DetectedInfo = info;
                    LastObservedTick = thisTick;
                    CurrentlyDetected = true;
                }

            }

            public Dictionary<long, ThreatInfo> CurrentThreats = new Dictionary<long, ThreatInfo>();

            readonly WcPbApi api;
            readonly DebugPanel sitconPanel, trackPanel;
            readonly IMyTerminalBlock me;
            readonly Dictionary<MyDetectedEntityInfo, float> threatsFromApi = new Dictionary<MyDetectedEntityInfo, float>();
            readonly float accelerationFilterCoeff;

            public long TrackedThreat = -1;

            public SituationalAwareness(WcPbApi api_, IMyTerminalBlock me_, float accelerationFilterCoeff_, DebugPanel sitconPanel_, DebugPanel trackPanel_)
            {
                api = api_;
                me = me_;
                accelerationFilterCoeff = accelerationFilterCoeff_;
                sitconPanel = sitconPanel_;
                trackPanel = trackPanel_;
                if (sitconPanel != null) sitconPanel.Title = "All hostiles:";
                if (trackPanel != null) trackPanel.Title = "Target track:";
            }

            public void UpdateThreatsFromWC(long currentTime)
            {
                trackPanel?.Reset();
                sitconPanel?.Reset();

                foreach (var threatInfo in CurrentThreats)
                    threatInfo.Value.CurrentlyDetected = false;

                threatsFromApi.Clear();
                api.GetSortedThreats(me, threatsFromApi);

                foreach (var threat in threatsFromApi.Keys)
                {
                    if (!CurrentThreats.ContainsKey(threat.EntityId)) CurrentThreats.Add(threat.EntityId, new ThreatInfo(accelerationFilterCoeff));

                    ThreatInfo threatInfo = CurrentThreats[threat.EntityId];
                    threatInfo.Update(threat, currentTime);

                    sitconPanel?.WriteLine($"{threat.Name}: {Math.Round(threatInfo.DetectedInfo.Position.X, 2)}, {Math.Round(threatInfo.DetectedInfo.Position.Y, 2)}, {Math.Round(threatInfo.DetectedInfo.Position.Z, 2)}");
                    if (trackPanel != null && threat.EntityId == TrackedThreat)
                    {
                        trackPanel?.WriteLine($"Name: {threatInfo.DetectedInfo.Name}");
                        trackPanel?.WriteLine($"Pos: {Math.Round(threatInfo.DetectedInfo.Position.X, 2)}, {Math.Round(threatInfo.DetectedInfo.Position.Y, 2)}, {Math.Round(threatInfo.DetectedInfo.Position.Z, 2)}");
                        trackPanel?.WriteLine($"Vel: {Math.Round(threatInfo.DetectedInfo.Velocity.X, 2)}, {Math.Round(threatInfo.DetectedInfo.Velocity.Y, 2)}, {Math.Round(threatInfo.DetectedInfo.Velocity.Z, 2)}");
                        trackPanel?.WriteLine($"Acc: {Math.Round(threatInfo.EstimatedAcceleration.X, 2)}, {Math.Round(threatInfo.EstimatedAcceleration.Y, 2)}, {Math.Round(threatInfo.EstimatedAcceleration.Z, 2)}");
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
