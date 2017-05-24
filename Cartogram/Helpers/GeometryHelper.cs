namespace Cartogram.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts;
    using Data;

    internal static class GeometryHelper
    {
        public static double GetArea(ICollection<IPoint> points)
        {
            if (points.Count < 3)
            {
                throw new ArgumentException("Cannot get area!");
            }

            double sum = 0;

            for (int i = 0; i < points.Count; i++)
            {
                sum += i < points.Count - 1
                       ? (points.ElementAt(i).Easting * points.ElementAt(i + 1).Northing) - (points.ElementAt(i + 1).Easting * points.ElementAt(i).Northing)
                       : (points.ElementAt(i).Easting * points.ElementAt(0).Northing) - (points.ElementAt(0).Easting * points.ElementAt(i).Northing);
            }

            double area = Math.Abs(sum * 0.5);

            return area;
        }

        public static IPoint GetGravityCenter(ICollection<IPoint> points)
        {
            if (points.Count < 3)
            {
                return null;
            }

            double reductionX = points.Min(p => p.Easting);
            double reductionY = points.Min(p => p.Northing);

            foreach (var point in points)
            {
                point.Easting -= reductionX;
                point.Northing -= reductionY;
            }

            double sx = 0;
            double sy = 0;
            double a = 0;

            for (int i = 0; i < points.Count; i++)
            {
                double p1 = i < points.Count - 1
                            ? points.ElementAt(i).Easting + points.ElementAt(i + 1).Easting
                            : points.ElementAt(i).Easting + points.ElementAt(0).Easting;
                double p2 = i < points.Count - 1
                            ? points.ElementAt(i).Northing + points.ElementAt(i + 1).Northing
                            : points.ElementAt(i).Northing + points.ElementAt(0).Northing;

                double p3 = i < points.Count - 1
                            ? (points.ElementAt(i).Easting * points.ElementAt(i + 1).Northing) - (points.ElementAt(i + 1).Easting * points.ElementAt(i).Northing)
                            : (points.ElementAt(i).Easting * points.ElementAt(0).Northing) - (points.ElementAt(0).Easting * points.ElementAt(i).Northing);

                sx += p1 * p3;
                sy += p2 * p3;
                a += p3;
            }

            a /= 2;

            double centerX = sx / (6 * a);
            double centerY = sy / (6 * a);

            IPoint gravityCenter = new ObservationPoint();
            gravityCenter.Northing = centerY + reductionY;
            gravityCenter.Easting = centerX + reductionX;

            return gravityCenter;
        }
    }
}