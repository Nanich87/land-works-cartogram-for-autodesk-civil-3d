namespace Cartogram.Helpers
{
    using System;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.Civil.DatabaseServices;

    internal static class PointHelper
    {
        public static Point3d GetNearestPoint(Point3dCollection pointCollection, Point3d point)
        {
            int nearestPointIndex = 0;
            double minDistance = Math.Sqrt(Math.Pow(point.X - pointCollection[0].X, 2) + Math.Pow(point.Y - pointCollection[0].Y, 2));

            for (int i = 1; i < pointCollection.Count; i++)
            {
                double checkDistance = Math.Sqrt(Math.Pow(point.X - pointCollection[i].X, 2) + Math.Pow(point.Y - pointCollection[i].Y, 2));
                if (checkDistance < minDistance)
                {
                    minDistance = checkDistance;
                    nearestPointIndex = i;
                }
            }

            return pointCollection[nearestPointIndex];
        }

        public static CogoPoint PromptForCogoPointSelection(string message, Transaction transaction)
        {
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;

            PromptEntityOptions promptEntityOptions = new PromptEntityOptions(string.Format("{0}: ", message));
            promptEntityOptions.SetRejectMessage(string.Format("Избраният обект не е Cogo Point!{0}", Environment.NewLine));
            promptEntityOptions.AddAllowedClass(typeof(CogoPoint), true);

            PromptEntityResult promptEntityResult = editor.GetEntity(promptEntityOptions);
            if (promptEntityResult.Status != PromptStatus.OK)
            {
                return null;
            }

            CogoPoint cogoPoint = transaction.GetObject(promptEntityResult.ObjectId, OpenMode.ForRead) as CogoPoint;

            return cogoPoint;
        }

        public static Point2d? PromptForPointSelection(string message)
        {
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;

            PromptPointOptions promptPointOptions = new PromptPointOptions(string.Format("\n{0}:", message));
            PromptPointResult promptPointResult = editor.GetPoint(promptPointOptions);
            if (promptPointResult.Status != PromptStatus.OK)
            {
                return null;
            }

            Point2d point = new Point2d(promptPointResult.Value.X, promptPointResult.Value.Y);

            return point;
        }
    }
}