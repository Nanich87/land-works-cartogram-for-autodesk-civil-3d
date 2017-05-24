namespace Cartogram.Data
{
    internal static class Scale
    {
        private static double scaleFactor;

        public static double GlobalScaleFactor
        {
            get
            {
                return Scale.scaleFactor;
            }

            set
            {
                Scale.scaleFactor = value;
            }
        }
    }
}