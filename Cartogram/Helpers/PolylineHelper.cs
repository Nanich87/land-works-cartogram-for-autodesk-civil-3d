namespace Cartogram.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Cartogram.Data;

    internal static class PolylineHelper
    {
        public static double GetSide(Point3d lineStartPoint, Point3d lineEndPoint, Point3d point)
        {
            return ((lineEndPoint.X - lineStartPoint.X) * (point.Y - lineStartPoint.Y)) - ((lineEndPoint.Y - lineStartPoint.Y) * (point.X - lineStartPoint.X));
        }

        public static void DrawElevationsText(Point3dCollection pointsCollection, bool drawZeroElevations = false)
        {
            Database database = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord blockTableRecord = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                for (int i = 0; i < pointsCollection.Count; i++)
                {
                    if (pointsCollection[i].Z == 0 && drawZeroElevations == false)
                    {
                        continue;
                    }

                    MText pointElevationText = new MText();
                    pointElevationText.Attachment = AttachmentPoint.BottomLeft;
                    pointElevationText.Location = pointsCollection[i];
                    pointElevationText.Contents = string.Format("{0:0}", pointsCollection[i].Z * 100);
                    pointElevationText.ColorIndex = 1;
                    pointElevationText.TextHeight = 1.5 * Scale.GlobalScaleFactor;

                    blockTableRecord.AppendEntity(pointElevationText);
                    transaction.AddNewlyCreatedDBObject(pointElevationText, true);
                }

                transaction.Commit();
            }
        }

        public static void DrawZeroElevetionLines(Point3dCollection points)
        {
            Database database = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                var blockTableRecord = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);

                Line zeroElevationLine = null;

                ICollection<Line> lines = new List<Line>();

                if (points.Count == 2)
                {
                    zeroElevationLine = PolylineHelper.CreateLine(points[0], points[1]);

                    lines.Add(zeroElevationLine);
                }
                else
                {
                    for (int i = 1; i < points.Count; i++)
                    {
                        zeroElevationLine = PolylineHelper.CreateLine(points[i - 1], points[i]);

                        lines.Add(zeroElevationLine);

                        if (i == points.Count - 1)
                        {
                            zeroElevationLine = PolylineHelper.CreateLine(points[i], points[0]);

                            lines.Add(zeroElevationLine);
                        }
                    }
                }

                foreach (Line line in lines)
                {
                    if (line != null)
                    {
                        line.ColorIndex = 1;
                        line.Linetype = "Dashed";
                        line.LinetypeScale = Scale.GlobalScaleFactor;

                        blockTableRecord.AppendEntity(line);
                        transaction.AddNewlyCreatedDBObject(line, true);
                    }
                }

                transaction.Commit();
            }
        }

        public static Point3dCollection GetZeroElevetionPointsFromPolyline(Point3dCollection polylineVertices)
        {
            Point3dCollection zeroElevationPointsCollection = new Point3dCollection();

            for (int vertexId = 1; vertexId < polylineVertices.Count; vertexId++)
            {
                Point3d[] zeroElevationPoints = PolylineHelper.GetZeroElevationPointsFromLine3d(polylineVertices[vertexId - 1], polylineVertices[vertexId]);

                for (int pointId = 0; pointId < zeroElevationPoints.Length; pointId++)
                {
                    if (zeroElevationPointsCollection.Contains(zeroElevationPoints[pointId]))
                    {
                        continue;
                    }

                    zeroElevationPointsCollection.Add(zeroElevationPoints[pointId]);
                }
            }

            return zeroElevationPointsCollection;
        }

        public static Point3dCollection GetVerticesFromPolyline3d(Polyline3d polyline)
        {
            Point3dCollection verticesCollection = new Point3dCollection();

            for (double i = 0; i < polyline.EndParam; i++)
            {
                Point3d vertex = polyline.GetPointAtParameter(i);
                verticesCollection.Add(vertex);
            }

            verticesCollection.Add(polyline.GetPointAtParameter(0));

            return verticesCollection;
        }

        public static Polyline3d PromptForPolyline3dSelection()
        {
            Database database = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;

            PromptEntityOptions promptEntityOptions = new PromptEntityOptions("Изберете 3D полилиния: ");
            promptEntityOptions.SetRejectMessage("Избраният обект не е 3D полилиния!");
            promptEntityOptions.AddAllowedClass(typeof(Polyline3d), false);

            PromptEntityResult promptEntityResult = editor.GetEntity(promptEntityOptions);

            if (promptEntityResult.Status != PromptStatus.OK)
            {
                return null;
            }

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                ObjectId polylineObjectId = promptEntityResult.ObjectId;

                Polyline3d polyline = transaction.GetObject(polylineObjectId, OpenMode.ForRead) as Polyline3d;

                return polyline;
            }
        }

        private static Line CreateLine(Point3d startPoint, Point3d endPoint)
        {
            Line line = new Line(startPoint, endPoint);

            return line;
        }

        private static Point3d[] GetZeroElevationPointsFromLine3d(Point3d lineStartPoint, Point3d lineEndPoint)
        {
            ICollection<Point3d> zeroElevationPointsCollection = new List<Point3d>();

            if (lineStartPoint.Z == 0)
            {
                zeroElevationPointsCollection.Add(lineStartPoint);
            }
            else if (lineEndPoint.Z == 0)
            {
                zeroElevationPointsCollection.Add(lineEndPoint);
            }
            else
            {
                Point3dCollection baseLinePointCollection = new Point3dCollection();
                baseLinePointCollection.Add(lineStartPoint);
                baseLinePointCollection.Add(lineEndPoint);

                Polyline3d baseLine = new Polyline3d(Poly3dType.SimplePoly, baseLinePointCollection, false);

                Point3d intersectionLineStartPoint = new Point3d(lineStartPoint.X, lineStartPoint.Y, 0);
                Point3d intersectionLineEndPoint = new Point3d(lineEndPoint.X, lineEndPoint.Y, 0);

                Point3dCollection intersectionLinePointsCollection = new Point3dCollection();
                intersectionLinePointsCollection.Add(intersectionLineStartPoint);
                intersectionLinePointsCollection.Add(intersectionLineEndPoint);

                Polyline3d intersectionLine = new Polyline3d(Poly3dType.SimplePoly, intersectionLinePointsCollection, false);

                Point3dCollection intersectionPoints = new Point3dCollection();

                baseLine.IntersectWith(intersectionLine, Intersect.OnBothOperands, intersectionPoints, new IntPtr(0), new IntPtr(0));

                foreach (Point3d intersectionPoint in intersectionPoints)
                {
                    zeroElevationPointsCollection.Add(intersectionPoint);
                }
            }

            return zeroElevationPointsCollection.ToArray();
        }
    }
}