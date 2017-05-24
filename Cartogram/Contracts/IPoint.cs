namespace Cartogram.Contracts
{
    using System;
    using System.Linq;

    internal interface IPoint
    {
        double Northing { get; set; }

        double Easting { get; set; }

        double Elevation { get; set; }
    }
}