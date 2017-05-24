namespace Cartogram.Helpers
{
    using System;

    internal static class AngleHelper
    {
        public static double ConvertGradiansToRadians(double angleInGradians)
        {
            double angleInRadians = angleInGradians * (Math.PI / 200);

            return angleInRadians;
        }

        public static double ConvertRadiansToGradians(double angleInRadians)
        {
            double angleInDradians = angleInRadians * (200 / Math.PI);

            return angleInDradians;
        }

        public static double GetSlopeLineRotationAngle(double polygonAngle, double slopeSide)
        {
            double slopeLineRotationAngle;

            if (polygonAngle < 200 && slopeSide < 0)
            {
                slopeLineRotationAngle = 200 - polygonAngle + (polygonAngle / 2);
            }
            else if (polygonAngle < 200 && slopeSide > 0)
            {
                slopeLineRotationAngle = polygonAngle / 2;
            }
            else if (polygonAngle > 200 && slopeSide < 0)
            {
                slopeLineRotationAngle = 100 - ((polygonAngle - 200) / 2);
            }
            else if (polygonAngle > 200 && slopeSide > 0)
            {
                slopeLineRotationAngle = 100 + ((polygonAngle - 200) / 2);
            }
            else
            {
                throw new ArgumentException("Не може да изчисли ъгъла на завъртане на линията на откоса!");
            }

            return slopeLineRotationAngle;
        }

        public static double GetPolygonAngle(double backsightHeadingAngle, double foresightHeadingAngle, double side)
        {
            double polygonAngle;

            if (backsightHeadingAngle > foresightHeadingAngle && side < 0)
            {
                polygonAngle = backsightHeadingAngle - foresightHeadingAngle;
            }
            else if (backsightHeadingAngle > foresightHeadingAngle && side > 0)
            {
                polygonAngle = 400 - backsightHeadingAngle + foresightHeadingAngle;
            }
            else if (backsightHeadingAngle < foresightHeadingAngle && side > 0)
            {
                polygonAngle = foresightHeadingAngle - backsightHeadingAngle;
            }
            else if (backsightHeadingAngle < foresightHeadingAngle && side < 0)
            {
                polygonAngle = backsightHeadingAngle - foresightHeadingAngle < 0
                                 ? backsightHeadingAngle - foresightHeadingAngle + 400
                                 : backsightHeadingAngle - foresightHeadingAngle;
            }
            else
            {
                throw new ArgumentException("Не може да изчисли полигоновия ъгъл!");
            }

            return polygonAngle;
        }
    }
}