namespace Hi3Helper.EncTool.WindowTool
{
    internal struct WindowRect
    {
        public int Left, Top, Right, Bottom;

        public int X { get => Left; set => Left = value; }
        public int Y { get => Top; set => Top = value; }
        public int Width
        {
            get => Right - Left;
            set => Right = Left + value;
        }
        public int Height
        {
            get => Bottom - Top;
            set => Bottom = Top + value;
        }
    }
}
