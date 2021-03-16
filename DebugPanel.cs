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
        public class DebugPanel
        {
            IMyTextSurface panel;
            string title;
            int maxLines;
            List<string> lines;
            StringBuilder contentBuilder;

            public DebugPanel(IMyTextSurface panel_, string title_, int maxLines_)
            {
                panel = panel_;
                contentBuilder = new StringBuilder(2048);
                maxLines = maxLines_;
                title = title_;

                lines = new List<string>(maxLines);
            }

            public void WriteLine(string newLine)
            {

                if (lines.Count == maxLines)
                    lines.Clear();
                lines.Add(newLine);

                contentBuilder.Clear();
                contentBuilder.Append(title);
                contentBuilder.Append('\n');
                foreach (var line in lines) 
                {
                    contentBuilder.Append(line);
                    contentBuilder.Append('\n');
                }
                panel.WriteText(contentBuilder);
            }

            public void Reset()
            {
                lines.Clear();
            }
        }
    }
}
