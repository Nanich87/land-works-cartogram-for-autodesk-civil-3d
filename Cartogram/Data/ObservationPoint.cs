namespace Cartogram.Data
{
    using Contracts;

    internal class ObservationPoint : IPoint
    {
        public int PointNumber { get; set; }

        public double Northing { get; set; }

        public double Easting { get; set; }

        public double Elevation { get; set; }
    }
}