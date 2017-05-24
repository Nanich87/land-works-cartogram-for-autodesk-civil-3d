namespace Cartogram
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Windows.Forms;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.AutoCAD.Runtime;
    using Autodesk.Civil.DatabaseServices;
    using Cartogram.Contracts;
    using Cartogram.Loggers;
    using Data;
    using Helpers;

    public class VerticalDesign
    {
        private static ILogger logger = new OnScreenLogger();

        // Scale

        [CommandMethod("CG_GetScale")]
        public static void GetScale()
        {
            double scale = 1000 / Scale.GlobalScaleFactor;

            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;
            editor.WriteMessage(string.Format("Мащаб: 1:{0:0}{1}", scale, Environment.NewLine));
        }

        [CommandMethod("CG_SetScale")]
        public static void SetScale()
        {
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;

            PromptDoubleOptions globalScaleFactorOptions = new PromptDoubleOptions("Въведете мащабен коефициент: ")
            {
                DefaultValue = 1.0,
                AllowNegative = false,
                AllowZero = false,
                UseDefaultValue = true,
                AllowNone = true
            };

            PromptDoubleResult globalScaleFactorResult = editor.GetDouble(globalScaleFactorOptions);

            if (globalScaleFactorResult.Status != PromptStatus.OK)
            {
                return;
            }

            double defaultGlobalScaleFactor = Scale.GlobalScaleFactor;

            if (double.TryParse(globalScaleFactorResult.Value.ToString(), out defaultGlobalScaleFactor))
            {
                Scale.GlobalScaleFactor = defaultGlobalScaleFactor;

                VerticalDesign.SetGlobalScaleFactor(defaultGlobalScaleFactor);
            }
        }

        // Import / Export Data

        [CommandMethod("CG_ImportData")]
        public static void ImportData()
        {
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Database.GetInstance().ImportFile(openFileDialog.FileName);

                    editor.WriteMessage(string.Format("Файлът {0} беше добавен успешно в базата данни!{1}", openFileDialog.SafeFileName, Environment.NewLine));
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
                }
            }
        }

        [CommandMethod("CG_ExportData")]
        public static void ExportData()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
            saveFileDialog.DefaultExt = "txt";
            saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string path = saveFileDialog.FileName;

                string contents = Database.GetInstance().ExportData();

                File.WriteAllText(path, contents, Encoding.Default);
            }
        }

        [CommandMethod("CG_SynchronizeData")]
        public static void SynchronizeData()
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = HostApplicationServices.WorkingDatabase;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;
            Transaction transaction = database.TransactionManager.StartTransaction();

            try
            {
                if (Database.GetInstance().Figures.Count == 0)
                {
                    editor.WriteMessage("Няма заредени данни за фигури!");

                    return;
                }

                if (Database.GetInstance().Elevations.Count == 0)
                {
                    editor.WriteMessage("Няма заредени данни за работни коти!");

                    return;
                }

                ObjectId[] blockReferenceIdCollection = BlockHelper.PromptForBlockSelection(editor, "Изберете фигури или работни коти: ");
                if (blockReferenceIdCollection == null)
                {
                    editor.WriteMessage("Невалидна селекция!");

                    return;
                }

                ICollection<string> figureHandlesCollection = new List<string>();
                ICollection<string> elevationHandlesCollection = new List<string>();

                foreach (ObjectId blockReferenceId in blockReferenceIdCollection)
                {
                    BlockReference blockReference = (BlockReference)transaction.GetObject(blockReferenceId, OpenMode.ForWrite);

                    if (blockReference.Name == Figure.FigureBlockName)
                    {
                        figureHandlesCollection.Add(blockReference.Handle.ToString());
                    }
                    else if (blockReference.Name == Elevation.ElevationBlockName)
                    {
                        elevationHandlesCollection.Add(blockReference.Handle.ToString());
                    }
                    else
                    {
                        continue;
                    }
                }

                transaction.Commit();

                int removedFiguresCount = Database.GetInstance().PurgeFigures(figureHandlesCollection);
                int removedElevationsCount = Database.GetInstance().PurgeElevations(elevationHandlesCollection);

                editor.WriteMessage(string.Format("{0} фигури бяха премахнати от базата данни{1}", removedFiguresCount, Environment.NewLine));
                editor.WriteMessage(string.Format("{0} работни коти бяха премахнати от базата данни{1}", removedElevationsCount, Environment.NewLine));
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
            }
            finally
            {
                transaction.Dispose();
            }
        }

        // Elevations

        [CommandMethod("CG_InsertElevation")]
        public static void InsertElevation()
        {
            VerticalDesign.GetGlobalScaleFactor();

            Autodesk.AutoCAD.DatabaseServices.Database database = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;
            Transaction transaction = database.TransactionManager.StartTransaction();

            try
            {
                TinSurface backgroundSurface = SurfaceHelper.PromptForSurfaceSelection("\nИзберете теренна повърхнина: ");
                if (backgroundSurface == null)
                {
                    editor.WriteMessage("Избраният обект не е TinSurface");

                    return;
                }

                editor.WriteMessage(string.Format("{0}", backgroundSurface.Name));

                backgroundSurface.Highlight();

                TinSurface designSurface = SurfaceHelper.PromptForSurfaceSelection("\nИзберете проектна повърхнина: ");
                if (designSurface == null)
                {
                    editor.WriteMessage("Избраният обект не е TinSurface");

                    return;
                }

                editor.WriteMessage(string.Format("{0}", designSurface.Name));

                designSurface.Highlight();

                PromptDoubleOptions promptDoubleOptions = new PromptDoubleOptions("\nВъведете дебелина на настилката: ")
                {
                    DefaultValue = 0,
                    AllowNegative = false,
                    AllowZero = true,
                    UseDefaultValue = true,
                    AllowNone = false
                };

                PromptDoubleResult promptDoubleResult = editor.GetDouble(promptDoubleOptions);
                if (promptDoubleResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("Невалидна стойност за дебелина на настилката");

                    return;
                }

                double ground = double.Parse(promptDoubleResult.Value.ToString());

                while (true)
                {
                    Point2d? topPoint = PointHelper.PromptForPointSelection("\nИзберете точка горе: ");
                    if (topPoint == null)
                    {
                        break;
                    }

                    Elevation elevation = new Elevation(backgroundSurface.FindElevationAtXY(topPoint.Value.X, topPoint.Value.Y), designSurface.FindElevationAtXY(topPoint.Value.X, topPoint.Value.Y), ground);
                    // elevation.ExistingElevation = backgroundSurface.FindElevationAtXY(topPoint.Value.X, topPoint.Value.Y);
                    // elevation.DesignElevation = designSurface.FindElevationAtXY(topPoint.Value.X, topPoint.Value.Y);
                    // elevation.Ground = ground;

                    Contracts.IPoint elevationInsertPoint = new ObservationPoint();
                    elevationInsertPoint.Easting = topPoint.Value.X;
                    elevationInsertPoint.Northing = topPoint.Value.Y;

                    elevation.Position.Easting = topPoint.Value.X;
                    elevation.Position.Northing = topPoint.Value.Y;

                    IDictionary<string, string> elevationBlockAttributes = new Dictionary<string, string>();
                    elevationBlockAttributes.Add("EG_ELEV", elevation.ExistingElevation.ToString("N4"));
                    elevationBlockAttributes.Add("DESIGN_ELEV_TOP", elevation.DesignElevation.ToString("N4"));
                    elevationBlockAttributes.Add("DESIGN_WORK", elevation.Ground.ToString("N3"));
                    elevationBlockAttributes.Add("CUT_FILL", (100 * (elevation.DesignElevation - elevation.Ground - elevation.ExistingElevation)).ToString("N0"));

                    string elevationHandle = BlockHelper.InsertBlock(Elevation.ElevationBlockName, elevationInsertPoint, Scale.GlobalScaleFactor, elevationBlockAttributes);

                    if (elevationHandle == string.Empty)
                    {
                        editor.WriteMessage("Не може да вмъкне блок!\n");

                        break;
                    }

                    Database.GetInstance().Elevations.Add(elevationHandle, elevation);

                    transaction.TransactionManager.QueueForGraphicsFlush();
                }

                backgroundSurface.Unhighlight();
                designSurface.Unhighlight();

                transaction.Commit();
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
            }
            finally
            {
                transaction.Dispose();
            }
        }

        [CommandMethod("CG_RemoveElevation")]
        public static void RemoveCartogramItem()
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = HostApplicationServices.WorkingDatabase;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;
            Transaction transaction = database.TransactionManager.StartTransaction();

            try
            {
                ObjectId[] blockReferenceIdCollection = BlockHelper.PromptForBlockSelection(editor, "Изберете обекти: ");
                if (blockReferenceIdCollection == null)
                {
                    editor.WriteMessage("Невалидна селекция!");

                    return;
                }

                ICollection<string> removedElevationHandles = new List<string>();

                foreach (ObjectId blockReferenceId in blockReferenceIdCollection)
                {
                    BlockReference blockReference = (BlockReference)transaction.GetObject(blockReferenceId, OpenMode.ForWrite);
                    BlockTableRecord blockTableRecord = (BlockTableRecord)transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead);
                    blockTableRecord.Dispose();

                    string handle = blockReference.Handle.ToString();

                    if (blockReference.Name == Elevation.ElevationBlockName && Database.GetInstance().ContainsElevation(handle))
                    {
                        blockReference.Erase();

                        Database.GetInstance().Elevations.Remove(handle);

                        removedElevationHandles.Add(handle);

                        editor.WriteMessage(string.Format("Блок ({0}) беше изтрит!{1}", handle, Environment.NewLine));
                    }
                }

                if (removedElevationHandles.Count > 0)
                {
                    VerticalDesign.UpdateFigures(removedElevationHandles);
                }

                transaction.Commit();
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
            }
            finally
            {
                transaction.Dispose();
            }
        }

        [CommandMethod("CG_CalculateElevationFromSurface")]
        public static void CalculateElevationFromSurface()
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = HostApplicationServices.WorkingDatabase;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;
            Transaction transaction = database.TransactionManager.StartTransaction();

            try
            {
                PromptIntegerOptions promptIntegerOptions = new PromptIntegerOptions("Въведете тип на повърхнината [1-Existing 2-Design]: ")
                {
                    DefaultValue = 0,
                    AllowNegative = false,
                    AllowZero = true,
                    UseDefaultValue = true,
                    AllowNone = false
                };

                PromptIntegerResult promptIntegerResult = editor.GetInteger(promptIntegerOptions);
                if (promptIntegerResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("Невалидна стойност за тип на повърхнината");

                    return;
                }

                IDictionary<int, string> surfaceTypes = new Dictionary<int, string>();
                surfaceTypes.Add(1, "EG_ELEV");
                surfaceTypes.Add(2, "DESIGN_ELEV_TOP");

                int surfaceType = int.Parse(promptIntegerResult.Value.ToString());
                if (!surfaceTypes.ContainsKey(surfaceType))
                {
                    editor.WriteMessage("Невалидна стойност за тип на повърхнината");

                    return;
                }

                TinSurface surface = SurfaceHelper.PromptForSurfaceSelection("Изберете повърхнина: ");
                if (surface == null)
                {
                    editor.WriteMessage("Избраният обект не е TinSurface");

                    return;
                }

                editor.WriteMessage(surface.Name);

                surface.Highlight();

                TypedValue[] blockReferenceFilterList = new TypedValue[1] { new TypedValue((int)DxfCode.Start, "INSERT") };
                SelectionFilter blockReferenceSelectionFilter = new SelectionFilter(blockReferenceFilterList);

                PromptSelectionOptions promptSelectionOptions = new PromptSelectionOptions();
                promptSelectionOptions.MessageForAdding = "Изберете работни коти: ";

                PromptSelectionResult promptSelectionResult = editor.GetSelection(promptSelectionOptions, blockReferenceSelectionFilter);
                if (promptSelectionResult.Status != PromptStatus.OK)
                {
                    return;
                }

                SelectionSet blockReferenceSelectionSet = promptSelectionResult.Value;
                ObjectId[] blockReferenceIdCollection = blockReferenceSelectionSet.GetObjectIds();

                ICollection<string> modifiedBlockReferences = new List<string>();

                foreach (ObjectId blockReferenceId in blockReferenceIdCollection)
                {
                    BlockReference blockReference = (BlockReference)transaction.GetObject(blockReferenceId, OpenMode.ForRead);
                    BlockTableRecord blockTableRecord = (BlockTableRecord)transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead);
                    blockTableRecord.Dispose();

                    if (!Database.GetInstance().Elevations.ContainsKey(blockReference.Handle.ToString()))
                    {
                        editor.WriteMessage(string.Format("Не може да намери данни за блок: {0}. Блокът се пропуска!{1}", blockReference.Handle.ToString(), Environment.NewLine));

                        continue;
                    }

                    if (Database.GetInstance().Elevations[blockReference.Handle.ToString()].ZeroPoint)
                    {
                        editor.WriteMessage(string.Format("Не може да изчисли нови данни за блок: {0}. Блокът се пропуска!{1}", blockReference.Handle.ToString(), Environment.NewLine));

                        continue;
                    }

                    modifiedBlockReferences.Add(blockReference.Handle.ToString());

                    double elevation = surface.FindElevationAtXY(blockReference.Position.X, blockReference.Position.Y);

                    switch (surfaceTypes[surfaceType])
                    {
                        case "EG_ELEV":
                            Database.GetInstance().Elevations[blockReference.Handle.ToString()].ExistingElevation = elevation;

                            break;
                        case "DESIGN_ELEV_TOP":
                            Database.GetInstance().Elevations[blockReference.Handle.ToString()].DesignElevation = elevation;

                            break;
                    }

                    AttributeCollection attributeCollection = blockReference.AttributeCollection;
                    foreach (ObjectId attributeId in attributeCollection)
                    {
                        AttributeReference attributeRefence = (AttributeReference)transaction.GetObject(attributeId, OpenMode.ForWrite);

                        if (attributeRefence.Tag == surfaceTypes[surfaceType])
                        {
                            attributeRefence.TextString = elevation.ToString("N4");
                        }
                        else if (attributeRefence.Tag == "CUT_FILL")
                        {
                            double workElevation = Database.GetInstance().Elevations[blockReference.Handle.ToString()].Value;

                            attributeRefence.TextString = string.Format("{0:0}", workElevation * 100);
                        }
                        else
                        {
                            continue;
                        }
                    }
                }

                transaction.Commit();

                surface.Unhighlight();

                VerticalDesign.UpdateFigures(modifiedBlockReferences);
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
            }
            finally
            {
                transaction.Dispose();
            }
        }

        [CommandMethod("CG_GetZeroPoint")]
        public static void GetZeroPoint()
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = HostApplicationServices.WorkingDatabase;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;
            Transaction transaction = database.TransactionManager.StartTransaction();

            try
            {
                ObjectId[] blockReferenceIdCollection = BlockHelper.PromptForBlockSelection(editor, "Изберете две работни коти за интерполация: ");
                if (blockReferenceIdCollection == null || blockReferenceIdCollection.Length != 2)
                {
                    editor.WriteMessage("Невалидна селекция!");

                    return;
                }

                double averageElevationSum = 0;

                ICollection<Contracts.IPoint> points = new List<Contracts.IPoint>();

                foreach (ObjectId blockReferenceId in blockReferenceIdCollection)
                {
                    BlockReference blockReference = (BlockReference)transaction.GetObject(blockReferenceId, OpenMode.ForRead);
                    BlockTableRecord blockTableRecord = (BlockTableRecord)transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead);
                    blockTableRecord.Dispose();

                    if (blockReference.Name != Elevation.ElevationBlockName)
                    {
                        continue;
                    }

                    AttributeCollection attributeCollection = blockReference.AttributeCollection;
                    foreach (ObjectId attributeId in attributeCollection)
                    {
                        AttributeReference attributeRefence = (AttributeReference)transaction.GetObject(attributeId, OpenMode.ForRead);
                        if (attributeRefence.Tag == "CUT_FILL")
                        {
                            averageElevationSum += double.Parse(attributeRefence.TextString);

                            ObservationPoint point = new ObservationPoint();
                            point.Northing = blockReference.Position.Y;
                            point.Easting = blockReference.Position.X;
                            point.Elevation = double.Parse(attributeRefence.TextString);

                            points.Add(point);
                        }
                    }
                }

                if (points.Count != 2)
                {
                    editor.WriteMessage("Не сте избрали две работни коти!");

                    return;
                }

                Point3d comparisonLineStartPoint = new Point3d(points.ElementAt(0).Easting, points.ElementAt(0).Northing, points.ElementAt(0).Elevation);
                Point3d comparisonEndPoint = new Point3d(points.ElementAt(1).Easting, points.ElementAt(1).Northing, points.ElementAt(1).Elevation);
                Line3d comparisonLine = new Line3d(comparisonLineStartPoint, comparisonEndPoint);

                Point3d baseLineStartPoint = new Point3d(points.ElementAt(0).Easting, points.ElementAt(0).Northing, 0);
                Point3d baseLineEndPoint = new Point3d(points.ElementAt(1).Easting, points.ElementAt(1).Northing, 0);
                Line3d baseLine = new Line3d(baseLineStartPoint, baseLineEndPoint);

                Point3d[] intersectionPoint = comparisonLine.IntersectWith(baseLine);

                Contracts.IPoint elevationInsertPoint = new ObservationPoint();
                elevationInsertPoint.Easting = intersectionPoint[0].X;
                elevationInsertPoint.Northing = intersectionPoint[0].Y;

                Elevation elevation = new Elevation();
                elevation.ZeroPoint = true;
                elevation.Position.Easting = intersectionPoint[0].X;
                elevation.Position.Northing = intersectionPoint[0].Y;

                IDictionary<string, string> elevationAttributes = new Dictionary<string, string>();
                elevationAttributes.Add("CUT_FILL", "0");

                string handle = BlockHelper.InsertBlock(Elevation.ElevationBlockName, elevationInsertPoint, Scale.GlobalScaleFactor, elevationAttributes);

                Database.GetInstance().Elevations.Add(handle, elevation);

                transaction.Commit();
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
            }
            finally
            {
                transaction.Dispose();
            }
        }

        // Figures
        [CommandMethod("CG_CreateFigure")]
        public static void CreateFigure()
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = HostApplicationServices.WorkingDatabase;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;
            Transaction transaction = database.TransactionManager.StartTransaction();

            try
            {
                ObjectId[] elevationBlockReferenceIdCollection = BlockHelper.PromptForBlockSelection(editor, "Изберете работни коти: ");
                if (elevationBlockReferenceIdCollection == null || elevationBlockReferenceIdCollection.Length < 3)
                {
                    editor.WriteMessage("Невалидна селекция!");

                    return;
                }

                Figure figure = new Figure();
                ICollection<Contracts.IPoint> elevationPoints = new List<Contracts.IPoint>();

                double averageElevationSum = 0;
                int positiveElevationsCount = 0;
                int negativeElevationsCount = 0;

                foreach (ObjectId elevationBlockReferenceId in elevationBlockReferenceIdCollection)
                {
                    BlockReference elevationBlockReference = (BlockReference)transaction.GetObject(elevationBlockReferenceId, OpenMode.ForRead);
                    BlockTableRecord elevationBlockTableRecord = (BlockTableRecord)transaction.GetObject(elevationBlockReference.BlockTableRecord, OpenMode.ForRead);
                    elevationBlockTableRecord.Dispose();

                    if (elevationBlockReference.Name != Elevation.ElevationBlockName)
                    {
                        continue;
                    }

                    AttributeCollection elevationAttributeCollection = elevationBlockReference.AttributeCollection;
                    foreach (ObjectId elevationAttributeId in elevationAttributeCollection)
                    {
                        AttributeReference elevationAttributeRefence = (AttributeReference)transaction.GetObject(elevationAttributeId, OpenMode.ForRead);

                        switch (elevationAttributeRefence.Tag)
                        {
                            case "CUT_FILL":
                                double workingElevation = double.Parse(elevationAttributeRefence.TextString);
                                averageElevationSum += workingElevation;

                                if (workingElevation > 0)
                                {
                                    positiveElevationsCount++;
                                }

                                if (workingElevation < 0)
                                {
                                    negativeElevationsCount++;
                                }

                                ObservationPoint point = new ObservationPoint();
                                point.Northing = elevationBlockReference.Position.Y;
                                point.Easting = elevationBlockReference.Position.X;

                                elevationPoints.Add(point);

                                figure.Elevations.Add(elevationBlockReference.Handle.ToString());

                                break;
                        }
                    }
                }

                if (elevationPoints.Count < 3)
                {
                    editor.WriteMessage("Не може да добави нова фигура с по-малко от 3 работни коти!");

                    return;
                }

                if (positiveElevationsCount > 0 && negativeElevationsCount > 0)
                {
                    editor.WriteMessage("Не може да създаде фигура с едновременно с положителни и отрицателни работни коти!");

                    return;
                }

                double figureAverageElevation = averageElevationSum / elevationPoints.Count;
                int figureHatchColor = figureAverageElevation >= 0 ? 1 : 2;

                figure.HatchHandle = HatchHelper.CreateHatch(elevationPoints, figureHatchColor, Scale.GlobalScaleFactor);

                Contracts.IPoint figureCentroid = GeometryHelper.GetGravityCenter(elevationPoints);

                double figureArea = GeometryHelper.GetArea(elevationPoints);
                double figureVolume = (figureAverageElevation / 100) * figureArea;

                IDictionary<string, string> figureAttributes = new Dictionary<string, string>();
                figureAttributes.Add("VOL", figureVolume.ToString("N0"));
                figureAttributes.Add("AVG", figureAverageElevation.ToString("N0"));
                figureAttributes.Add("NMB_V", Figure.NextFigureNumber.ToString());
                figureAttributes.Add("AREA", figureArea.ToString("N0"));
                figureAttributes.Add("NMB", Figure.NextFigureNumber.ToString());

                string figureBlockHandler = BlockHelper.InsertBlock(Figure.FigureBlockName, figureCentroid, Scale.GlobalScaleFactor, figureAttributes);

                figure.Number = Figure.NextFigureNumber;
                Database.GetInstance().Figures.Add(figureBlockHandler, figure);

                Figure.NextFigureNumber++;

                transaction.Commit();
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
            }
            finally
            {
                transaction.Dispose();
            }
        }

        [CommandMethod("CG_RemoveFigure")]
        public static void RemoveFigure()
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = HostApplicationServices.WorkingDatabase;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;
            Transaction transaction = database.TransactionManager.StartTransaction();

            try
            {
                ObjectId[] blockReferenceIdCollection = BlockHelper.PromptForBlockSelection(editor, "Изберете обекти: ");
                if (blockReferenceIdCollection == null)
                {
                    editor.WriteMessage("Невалидна селекция!");

                    return;
                }

                foreach (ObjectId blockReferenceId in blockReferenceIdCollection)
                {
                    BlockReference blockReference = (BlockReference)transaction.GetObject(blockReferenceId, OpenMode.ForWrite);
                    BlockTableRecord blockTableRecord = (BlockTableRecord)transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead);
                    blockTableRecord.Dispose();

                    string handle = blockReference.Handle.ToString();

                    if (blockReference.Name == Figure.FigureBlockName && Database.GetInstance().ContainsFigure(handle))
                    {
                        long hatchHandleValue = Convert.ToInt64(Database.GetInstance().Figures[handle].HatchHandle, 16);

                        Handle hatchHandle = new Handle(hatchHandleValue);

                        ObjectId hatchObjectId = database.GetObjectId(false, hatchHandle, 0);

                        Hatch hatch = (Hatch)transaction.GetObject(hatchObjectId, OpenMode.ForWrite);
                        if (hatch != null)
                        {
                            hatch.Erase();
                        }

                        blockReference.Erase();

                        Database.GetInstance().Figures.Remove(handle);

                        editor.WriteMessage(string.Format("Блок ({0}) беше изтрит!{1}", blockReference.Handle.ToString(), Environment.NewLine));
                    }
                }

                transaction.Commit();
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
            }
            finally
            {
                transaction.Dispose();
            }
        }

        [CommandMethod("CG_RenumberFigures")]
        public static void RenumberFigures()
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = HostApplicationServices.WorkingDatabase;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;
            Transaction transaction = database.TransactionManager.StartTransaction();

            int nextFigureNumber = 0;

            try
            {
                ObjectId[] blockReferenceIdCollection = BlockHelper.PromptForBlockSelection(editor, "Изберете количества: ");
                if (blockReferenceIdCollection == null)
                {
                    editor.WriteMessage("Невалидна селекция!");

                    return;
                }

                foreach (ObjectId blockReferenceId in blockReferenceIdCollection)
                {
                    BlockReference blockReference = (BlockReference)transaction.GetObject(blockReferenceId, OpenMode.ForWrite);

                    if (blockReference.Name != Figure.FigureBlockName)
                    {
                        continue;
                    }

                    string handle = blockReference.ToString();

                    BlockTableRecord blockTableRecord = (BlockTableRecord)transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead);
                    blockTableRecord.Dispose();

                    AttributeCollection attributeCollection = blockReference.AttributeCollection;

                    nextFigureNumber++;

                    foreach (ObjectId attributeId in attributeCollection)
                    {
                        AttributeReference attributeRefence = (AttributeReference)transaction.GetObject(attributeId, OpenMode.ForWrite);
                        switch (attributeRefence.Tag)
                        {
                            case "NMB":
                                attributeRefence.TextString = nextFigureNumber.ToString();
                                break;
                            case "NMB_V":
                                attributeRefence.TextString = nextFigureNumber.ToString();
                                break;
                        }
                    }

                    if (Database.GetInstance().ContainsFigure(handle))
                    {
                        Database.GetInstance().Figures[handle].Number = nextFigureNumber;
                    }
                }

                transaction.Commit();

                editor.WriteMessage(string.Format("Преномерирани фигури: {0}{1}", nextFigureNumber, Environment.NewLine));

                Figure.NextFigureNumber = nextFigureNumber;
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
            }
            finally
            {
                transaction.Dispose();
            }
        }

        [CommandMethod("CG_SetNextFigureNumber")]
        public static void SetNextFigureNumber()
        {
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;

            PromptIntegerOptions promptIntegerOptions = new PromptIntegerOptions("Въведете следващ номер на фигура: ")
            {
                DefaultValue = Figure.NextFigureNumber + 1,
                AllowNegative = false,
                AllowZero = true,
                UseDefaultValue = true,
                AllowNone = false
            };

            PromptIntegerResult promptIntegerResult = editor.GetInteger(promptIntegerOptions);

            if (promptIntegerResult.Status != PromptStatus.OK)
            {
                return;
            }

            Figure.NextFigureNumber = int.Parse(promptIntegerResult.Value.ToString());
        }

        // Volumes

        [CommandMethod("CG_GetVolumeReport")]
        public static void GetVolumeReport()
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = HostApplicationServices.WorkingDatabase;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;
            Transaction transaction = database.TransactionManager.StartTransaction();

            try
            {
                ObjectId[] blockReferenceIdCollection = BlockHelper.PromptForBlockSelection(editor, "Изберете количества: ");
                if (blockReferenceIdCollection == null)
                {
                    editor.WriteMessage("Невалидна селекция!");

                    return;
                }

                IDictionary<string, double> volumes = new Dictionary<string, double>();
                volumes.Add("Cut1", 0.0);
                volumes.Add("Cut2", 0.0);
                volumes.Add("Cut3", 0.0);
                volumes.Add("Fill", 0.0);

                foreach (ObjectId blockReferenceId in blockReferenceIdCollection)
                {
                    BlockReference blockReference = (BlockReference)transaction.GetObject(blockReferenceId, OpenMode.ForRead);

                    if (blockReference.Name != "CG_Figure")
                    {
                        continue;
                    }

                    BlockTableRecord blockTableRecord = (BlockTableRecord)transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead);
                    blockTableRecord.Dispose();

                    AttributeCollection attributeCollection = blockReference.AttributeCollection;

                    IDictionary<string, double> data = new Dictionary<string, double>();
                    data.Add("VOL", 0);
                    data.Add("AVG", 0);
                    data.Add("NMB", 0);
                    data.Add("NMB_V", 0);
                    data.Add("AREA", 0);

                    foreach (ObjectId attributeId in attributeCollection)
                    {
                        AttributeReference attributeRefence = (AttributeReference)transaction.GetObject(attributeId, OpenMode.ForRead);
                        switch (attributeRefence.Tag)
                        {
                            case "VOL":
                            case "AREA":
                                data[attributeRefence.Tag] = double.Parse(attributeRefence.TextString);
                                break;
                            case "NMB":
                            case "NMB_V":
                            case "AVG":
                                data[attributeRefence.Tag] = int.Parse(attributeRefence.TextString);
                                break;
                        }
                    }

                    if (data["AVG"] >= 0)
                    {
                        volumes["Fill"] += data["VOL"];
                    }
                    else if (-15 <= data["AVG"] && data["AVG"] < 0)
                    {
                        volumes["Cut1"] += Math.Abs(data["VOL"]);
                    }
                    else if (-50 <= data["AVG"] && data["AVG"] < -15)
                    {
                        volumes["Cut2"] += Math.Abs(data["VOL"]);
                    }
                    else
                    {
                        volumes["Cut3"] += Math.Abs(data["VOL"]);
                    }
                }

                editor.WriteMessage(string.Format("Тънки изкопи до 0.15: {0}{4}Тънки изкопи от 0.15 до 0.50: {1}{4}Масов изкоп: {2}{4}Насип: {3}{4}", volumes["Cut1"], volumes["Cut2"], volumes["Cut3"], volumes["Fill"], Environment.NewLine));
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
            }
            finally
            {
                transaction.Dispose();
            }
        }

        [CommandMethod("VD_CartogramDrawZeroElevationLines")]
        public static void CreateCartograma()
        {
            VerticalDesign.GetGlobalScaleFactor();

            Polyline3d selectedPolyline = PolylineHelper.PromptForPolyline3dSelection();
            Point3dCollection polylineVertices = PolylineHelper.GetVerticesFromPolyline3d(selectedPolyline);
            Point3dCollection intersectionPoints = PolylineHelper.GetZeroElevetionPointsFromPolyline(polylineVertices);

            PolylineHelper.DrawZeroElevetionLines(intersectionPoints);
            PolylineHelper.DrawElevationsText(polylineVertices);
            PolylineHelper.DrawElevationsText(intersectionPoints, true);
        }

        private static void GetGlobalScaleFactor()
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;

            try
            {
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    DBDictionary nod = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);

                    if (nod.Contains("GlobalScaleFactor"))
                    {
                        ObjectId scaleFactorObjectId = nod.GetAt("GlobalScaleFactor");

                        Xrecord scaleFactorRecord = (Xrecord)transaction.GetObject(scaleFactorObjectId, OpenMode.ForRead);

                        foreach (TypedValue scaleData in scaleFactorRecord.Data)
                        {
                            if (scaleData.TypeCode == 46)
                            {
                                Scale.GlobalScaleFactor = (double)scaleData.Value;
                            }
                        }
                    }

                    transaction.Commit();
                }
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
            }
            finally
            {
                database.Dispose();
            }
        }

        private static void SetGlobalScaleFactor(double scale)
        {
            Autodesk.AutoCAD.DatabaseServices.Database database = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;

            try
            {
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    DBDictionary nod = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForWrite);

                    Xrecord scaleFactorRecord = new Xrecord();
                    scaleFactorRecord.Data = new ResultBuffer(new TypedValue((int)DxfCode.ShapeScale, scale));

                    nod.SetAt("GlobalScaleFactor", scaleFactorRecord);

                    transaction.AddNewlyCreatedDBObject(scaleFactorRecord, true);
                    transaction.Commit();
                }
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage(string.Format("Грешка: {0}{1}", ex.Message, Environment.NewLine));
            }
            finally
            {
                database.Dispose();
            }
        }

        private static void UpdateFigures(ICollection<string> elevationHandles)
        {
            Autodesk.AutoCAD.ApplicationServices.Document document = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            Editor editor = document.Editor;
            Autodesk.AutoCAD.DatabaseServices.Database database = HostApplicationServices.WorkingDatabase;

            ICollection<string> modifiedFigureHandles = Database.GetInstance().GetAffectedFigures(elevationHandles);

            int erasedFiguresCount = 0;

            foreach (var modifiedFigureHandle in modifiedFigureHandles)
            {
                FigureData figureData = Database.GetInstance().GetFigureData(modifiedFigureHandle);

                long blockHandler = Convert.ToInt64(modifiedFigureHandle, 16);

                Handle figureHandle = new Handle(blockHandler);

                ObjectId figureObjectId = database.GetObjectId(false, figureHandle, 0);

                using (Transaction transaction = document.TransactionManager.StartTransaction())
                {
                    BlockReference blockReference = (BlockReference)transaction.GetObject(figureObjectId, OpenMode.ForWrite);
                    BlockTableRecord blockTableRecord = (BlockTableRecord)transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead);
                    blockTableRecord.Dispose();

                    if (!figureData.IsDestroyed && figureData.SingleFigure)
                    {
                        AttributeCollection attributeCollection = blockReference.AttributeCollection;

                        foreach (ObjectId attributeId in attributeCollection)
                        {
                            AttributeReference attributeRefence = (AttributeReference)transaction.GetObject(attributeId, OpenMode.ForWrite);
                            switch (attributeRefence.Tag)
                            {
                                case "VOL":
                                    attributeRefence.TextString = figureData.Volume.ToString("N0");
                                    break;
                                case "AVG":
                                    attributeRefence.TextString = (figureData.AverageElevation * 100).ToString("N0");
                                    break;
                            }
                        }
                    }
                    else
                    {
                        long hatchHandler = Convert.ToInt64(Database.GetInstance().Figures[modifiedFigureHandle].HatchHandle, 16);

                        Handle hatchHandle = new Handle(hatchHandler);

                        ObjectId hatchObjectId = database.GetObjectId(false, hatchHandle, 0);

                        Hatch hatch = (Hatch)transaction.GetObject(hatchObjectId, OpenMode.ForWrite);
                        hatch.Erase();

                        blockReference.Erase();

                        Database.GetInstance().Figures.Remove(modifiedFigureHandle);

                        erasedFiguresCount++;
                    }

                    transaction.Commit();
                }
            }

            if (erasedFiguresCount > 0)
            {
                editor.WriteMessage(string.Format("Внимание: {0} фигури бяха изтрити{1}", erasedFiguresCount, Environment.NewLine));
            }
        }
    }
}