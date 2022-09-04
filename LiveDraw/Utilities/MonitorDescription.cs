using WpfScreenHelper;

namespace AntFu7.LiveDraw.Utilities
{
    public class MonitorDescription
    {
        public Screen Screen { get; }
        public string ToolTip { get; }
        public int Number { get; }

        public MonitorDescription(Screen screen, int number, string tooltip)
        {
            Screen = screen;
            Number = number;
            ToolTip = tooltip;
        }
    }
}