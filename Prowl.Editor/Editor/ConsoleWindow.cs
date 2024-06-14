﻿using Prowl.Editor.Preferences;
using Prowl.Icons;
using Prowl.Runtime;
using Prowl.Runtime.GUI;
using Prowl.Runtime.GUI.Graphics;

namespace Prowl.Editor
{
    public class ConsoleWindow : EditorWindow
    {
        protected override double Width { get; } = 512 + (512 / 2);
        protected override double Height { get; } = 256;

        private uint _logCount;
        private readonly List<LogMessage> _logMessages;
        private int _maxLogs = 100;

        public ConsoleWindow() : base()
        {
            Title = FontAwesome6.Terminal + " Console";
            _logMessages = new List<LogMessage>();
            Debug.OnLog += OnLog;
        }

        private void OnLog(string message, LogSeverity logSeverity)
        {
            if (logSeverity == LogSeverity.Normal && !GeneralPreferences.Instance.ShowDebugLogs) return;
            else if (logSeverity == LogSeverity.Warning && !GeneralPreferences.Instance.ShowDebugWarnings) return;
            else if (logSeverity == LogSeverity.Error && !GeneralPreferences.Instance.ShowDebugErrors) return;
            else if (logSeverity == LogSeverity.Success && !GeneralPreferences.Instance.ShowDebugSuccess) return;

            _logMessages.Add(new LogMessage(message, logSeverity));
            if (_logMessages.Count > _maxLogs)
                _logMessages.RemoveAt(0);
            _logCount++;
        }

        protected override void Draw()
        {
            gui.CurrentNode.Layout(LayoutType.Column);
            gui.CurrentNode.ScaleChildren();

            //using(g.Node().Width(Size.Percentage(1f)).MaxHeight(20).Enter())
            //{
            //    g.DrawRectFilled(g.CurrentNode.LayoutData.Rect, GuiStyle.SelectedColor);
            //}
            using (gui.Node("List").Width(Size.Percentage(1f)).Padding(0, 3, 3, 3).Clip().Enter())
            {
                double height = 0;
                for (int i = _logMessages.Count; i-- > 0;)
                {
                    var logSeverity = _logMessages[i].LogSeverity;
                    if (logSeverity == LogSeverity.Normal && !GeneralPreferences.Instance.ShowDebugLogs) continue;
                    else if (logSeverity == LogSeverity.Warning && !GeneralPreferences.Instance.ShowDebugWarnings) continue;
                    else if (logSeverity == LogSeverity.Error && !GeneralPreferences.Instance.ShowDebugErrors) continue;
                    else if (logSeverity == LogSeverity.Success && !GeneralPreferences.Instance.ShowDebugSuccess) continue;

                    int width = (int)gui.CurrentNode.LayoutData.InnerRect.width;
                    var pos = gui.CurrentNode.LayoutData.InnerRect.Position;
                    pos.y -= gui.CurrentNode.LayoutData.VScroll;
                    var size = UIDrawList.DefaultFont.CalcTextSize(_logMessages[i].Message, 0, width - 24);

                    gui.Draw2D.DrawLine(new(pos.x + 12, pos.y + height), new(pos.x + width - 12, pos.y + height), GuiStyle.Borders, 1);

                    _logMessages[i].Draw(pos + new Vector2(12, height + 8), width - 24);
                    height += size.y + 8;
                }

                // Dummy node to set the height of the scroll area
                gui.Node("Dummy").Width(5).Height(height);

                gui.ScrollV();
            }
        }

        private record LogMessage(string Message, LogSeverity LogSeverity)
        {
            public readonly string Message = Message;
            public readonly LogSeverity LogSeverity = LogSeverity;

            public void Draw(Vector2 position, double wrapWidth)
            {
                var color = ToColor(LogSeverity);
                Gui.ActiveGUI.Draw2D.DrawText(UIDrawList.DefaultFont, Message, 20, position, color, wrapWidth);
            }

            private static System.Numerics.Vector4 ToColor(LogSeverity logSeverity) => logSeverity switch {
                LogSeverity.Normal => new System.Numerics.Vector4(1, 1, 1, 1),
                LogSeverity.Success => new System.Numerics.Vector4(0, 1, 0, 1),
                LogSeverity.Warning => new System.Numerics.Vector4(1, 1, 0, 1),
                LogSeverity.Error => new System.Numerics.Vector4(1, 0, 0, 1),
                _ => throw new NotImplementedException("log level not implemented")
            };
        }
    }
}