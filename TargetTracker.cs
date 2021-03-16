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
        public class TargetTracker
        {
            public bool Enable;

            SituationalAwareness sitcon;

            Action<string> debug;
            IMyTerminalBlock forwardReference;
            DirectionController controller;

            long targetId;
            Vector3D targetPosition;

            public TargetTracker(SituationalAwareness sitcon_, DirectionController _controller, IMyTerminalBlock _fwdRef, Action<string> _debug)
            {
                Enable = false;
                sitcon = sitcon_;
                controller = _controller;
                forwardReference = _fwdRef;
                debug = _debug;
                targetId = -1;
            }

            public void SetTarget(long targetId_)
            {
                targetId = targetId_;
                targetPosition = sitcon.GetLastLocation(targetId).Value;
            }

            public void SetTarget(Vector3D target) => targetPosition = target;

            public void Update1()
            {
                if (!Enable) return;

                Vector3D desiredDirection = targetPosition - forwardReference.WorldMatrix.Translation;
                desiredDirection.Normalize();

                Vector3D directionLocal = Vector3D.TransformNormal(desiredDirection, MatrixD.Transpose(forwardReference.WorldMatrix));
                controller.Direction = directionLocal;
            }

            public void Update10()
            {
                if (!Enable) return;

                if (targetId != -1)
                    targetPosition = sitcon.GetLastLocation(targetId).Value;
            }
        }
    }
}
