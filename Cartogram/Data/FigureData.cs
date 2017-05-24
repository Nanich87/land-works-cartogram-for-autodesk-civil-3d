namespace Cartogram.Data
{
    internal struct FigureData
    {
        public double AverageElevation;

        public double Area;

        public bool SingleFigure;

        public bool IsDestroyed;

        public double Volume
        {
            get
            {
                return this.Area * this.AverageElevation;
            }
        }
    }
}