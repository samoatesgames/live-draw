namespace AntFu7.LiveDraw.Utilities
{
    public class MonitorDescription
    {
        public string ToolTip { get; }
        public int Number { get; }

        public MonitorDescription(int number, string tooltip)
        {
            Number = number;
            ToolTip = tooltip;
        }
    }
}