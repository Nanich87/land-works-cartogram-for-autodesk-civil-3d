namespace Cartogram.Helpers
{
    using System;
    using System.Collections.Generic;
    using Cartogram.Contracts;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;

    internal static class BlockHelper
    {
        public static ObjectId[] PromptForBlockSelection(Editor editor, string message)
        {
            TypedValue[] blockFilterList = new TypedValue[1]
            {
                new TypedValue((int)DxfCode.Start, "INSERT")
            };
            SelectionFilter blockelectionFilter = new SelectionFilter(blockFilterList);

            PromptSelectionOptions promptSelectionOptions = new PromptSelectionOptions();
            promptSelectionOptions.MessageForAdding = message;

            PromptSelectionResult promptSelectionResult = editor.GetSelection(promptSelectionOptions, blockelectionFilter);
            if (promptSelectionResult.Status != PromptStatus.OK)
            {
                return null;
            }

            SelectionSet blockSelectionSet = promptSelectionResult.Value;
            ObjectId[] objectIdCollection = blockSelectionSet.GetObjectIds();

            return objectIdCollection;
        }

        public static string InsertBlock(string name, IPoint insertPoint, double scale, IDictionary<string, string> attributesCollection)
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = HostApplicationServices.WorkingDatabase;
            Transaction transaction = database.TransactionManager.StartTransaction();

            BlockTable blockTable = database.BlockTableId.GetObject(OpenMode.ForRead) as BlockTable;

            if (!blockTable.Has(name))
            {
                return string.Empty;
            }

            BlockTableRecord blockDefinition = blockTable[name].GetObject(OpenMode.ForWrite) as BlockTableRecord;
            BlockTableRecord modelSpaceBlockTableRecord = blockTable[BlockTableRecord.ModelSpace].GetObject(OpenMode.ForWrite) as BlockTableRecord;

            Point3d blockReferenceInsertPoint = new Point3d(insertPoint.Easting, insertPoint.Northing, 0);

            using (BlockReference blockReference = new BlockReference(blockReferenceInsertPoint, blockDefinition.ObjectId))
            {
                blockReference.ScaleFactors = new Scale3d(scale);

                modelSpaceBlockTableRecord.AppendEntity(blockReference);

                transaction.AddNewlyCreatedDBObject(blockReference, true);

                foreach (ObjectId blockDefinitionId in blockDefinition)
                {
                    DBObject databaseObject = blockDefinitionId.GetObject(OpenMode.ForRead);

                    AttributeDefinition blockAttributeDefinition = databaseObject as AttributeDefinition;
                    if ((blockAttributeDefinition != null) && (!blockAttributeDefinition.Constant))
                    {
                        using (AttributeReference attributeReference = new AttributeReference())
                        {
                            attributeReference.SetAttributeFromBlock(blockAttributeDefinition, blockReference.BlockTransform);

                            if (attributesCollection.ContainsKey(attributeReference.Tag))
                            {
                                attributeReference.TextString = attributesCollection[attributeReference.Tag];
                            }

                            blockReference.AttributeCollection.AppendAttribute(attributeReference);

                            transaction.AddNewlyCreatedDBObject(attributeReference, true);
                        }
                    }
                }

                transaction.Commit();

                return blockReference.Handle.ToString();
            }
        }

        public static Point2d GetTopPointPosition(BlockReference blockReference)
        {
            double positionX = 0.0;
            double positionY = 0.0;

            DynamicBlockReferencePropertyCollection propertyCollection = blockReference.DynamicBlockReferencePropertyCollection;

            foreach (DynamicBlockReferenceProperty item in propertyCollection)
            {
                switch (item.PropertyName)
                {
                    case "Position2 X":
                        positionX = (double)item.Value;
                        break;
                    case "Position2 Y":
                        positionY = (double)item.Value;
                        break;
                }
            }

            Point2d point = new Point2d(positionX + blockReference.Position.X, positionY + blockReference.Position.Y);

            return point;
        }

        public static void SetTopPointPosition(BlockReference blockReference, Point3d blockInsertPoint, Point2d? topPoint)
        {
            DynamicBlockReferencePropertyCollection propertyCollection = blockReference.DynamicBlockReferencePropertyCollection;

            foreach (DynamicBlockReferenceProperty item in propertyCollection)
            {
                switch (item.PropertyName)
                {
                    case "Position2 X":
                        item.Value = topPoint.Value.X - blockInsertPoint.X;
                        break;
                    case "Position2 Y":
                        item.Value = topPoint.Value.Y - blockInsertPoint.Y;
                        break;
                }
            }
        }
    }
}