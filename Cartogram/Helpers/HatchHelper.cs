namespace Cartogram.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cartogram.Contracts;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.Geometry;

    internal static class HatchHelper
    {
        public static string CreateHatch(ICollection<IPoint> boundaryPoints, int colorIndex, double scale)
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = HostApplicationServices.WorkingDatabase;
            Transaction transaction = database.TransactionManager.StartTransaction();

            ObjectId modelSpaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
            BlockTableRecord blockTableRecord = transaction.GetObject(modelSpaceId, OpenMode.ForWrite) as BlockTableRecord;

            Polyline polylineBoundary = new Polyline(boundaryPoints.Count);
            polylineBoundary.Normal = Vector3d.ZAxis;

            for (int i = 0; i < boundaryPoints.Count; i++)
            {
                Point2d boundaryPoint = new Point2d(boundaryPoints.ElementAt(i).Easting, boundaryPoints.ElementAt(i).Northing);

                polylineBoundary.AddVertexAt(i, boundaryPoint, 0.0, -1.0, -1.0);
            }

            polylineBoundary.Closed = true;

            ObjectId polylineBoundaryId = blockTableRecord.AppendEntity(polylineBoundary);

            transaction.AddNewlyCreatedDBObject(polylineBoundary, true);

            ObjectIdCollection objectIdCollection = new ObjectIdCollection();
            objectIdCollection.Add(polylineBoundaryId);

            Hatch hatch = new Hatch();

            Vector3d normal = new Vector3d(0.0, 0.0, 1.0);

            hatch.Normal = normal;
            hatch.Elevation = 0.0;
            hatch.PatternScale = scale;
            hatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
            hatch.ColorIndex = colorIndex;

            blockTableRecord.AppendEntity(hatch);

            transaction.AddNewlyCreatedDBObject(hatch, true);

            hatch.Associative = true;
            hatch.AppendLoop((int)HatchLoopTypes.Default, objectIdCollection);
            hatch.EvaluateHatch(true);

            transaction.Commit();

            return hatch.Handle.ToString();
        }
    }
}