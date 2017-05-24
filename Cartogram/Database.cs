namespace Cartogram
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Cartogram.Contracts;
    using Cartogram.Data;
    using Cartogram.Helpers;

    internal class Database
    {
        private static readonly Database instance = new Database();

        private readonly ICollection<Ground> grounds = new List<Ground>();
        private readonly IDictionary<string, Figure> figures = new Dictionary<string, Figure>();
        private readonly IDictionary<string, Elevation> elevations = new Dictionary<string, Elevation>();

        private Database()
        {
        }

        public IDictionary<string, Elevation> Elevations
        {
            get
            {
                return this.elevations;
            }
        }

        public IDictionary<string, Figure> Figures
        {
            get
            {
                return this.figures;
            }
        }

        public ICollection<Ground> Grounds
        {
            get
            {
                return this.grounds;
            }
        }

        public static Database GetInstance()
        {
            return Database.instance;
        }

        public int PurgeFigures(ICollection<string> handles)
        {
            int purgedFiguresCount = 0;

            foreach (var figure in this.Figures.ToList())
            {
                if (!handles.Contains(figure.Key))
                {
                    this.Figures.Remove(figure.Key);

                    purgedFiguresCount++;
                }
            }

            return purgedFiguresCount;
        }

        public int PurgeElevations(ICollection<string> handles)
        {
            int purgedElevationsCount = 0;

            foreach (var elevation in this.Elevations.ToList())
            {
                if (!handles.Contains(elevation.Key))
                {
                    this.Elevations.Remove(elevation.Key);

                    purgedElevationsCount++;
                }
            }

            return purgedElevationsCount;
        }

        public ICollection<string> GetAffectedFigures(ICollection<string> elevationHandles)
        {
            ICollection<string> affectedFigureHandles = new List<string>();

            foreach (var figure in this.Figures)
            {
                bool isAffected = figure.Value.Elevations.Intersect(elevationHandles).Any();

                if (isAffected && !affectedFigureHandles.Contains(figure.Key))
                {
                    affectedFigureHandles.Add(figure.Key);
                }
            }

            return affectedFigureHandles;
        }

        public string ExportData()
        {
            StringBuilder output = new StringBuilder();

            foreach (var figure in this.Figures)
            {
                output.AppendFormat("FIGURE {0} {1} {2}{3}", figure.Key, figure.Value.Number, figure.Value.HatchHandle, Environment.NewLine);
                output.AppendFormat("{0}{1}", string.Join(";", figure.Value.Elevations), Environment.NewLine);
            }

            foreach (var elevation in this.Elevations)
            {
                output.AppendFormat("ELEVATION {0}{1}", elevation.Key, elevation.Value.ZeroPoint, Environment.NewLine);
                output.AppendFormat(
                    "{0:0.0000} {1:0.0000} {2:0.000} {3:0.0000} {4:0.0000} {5}{6}",
                    elevation.Value.ExistingElevation,
                    elevation.Value.DesignElevation,
                    elevation.Value.Ground,
                    elevation.Value.Position.Northing,
                    elevation.Value.Position.Easting,
                    Environment.NewLine);
            }

            return output.ToString();
        }

        public void ImportFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new ArgumentException(string.Format("Не може да намери файла {0}!", path));
            }

            using (StreamReader reader = new StreamReader(path, Encoding.Default))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    string[] data = line.Split(' ');

                    if (data.Length == 0)
                    {
                        continue;
                    }

                    switch (data[0])
                    {
                        case "FIGURE":
                            if (this.ContainsFigure(data[1]))
                            {
                                continue;
                            }

                            Figure figure = new Figure();
                            figure.Number = int.Parse(data[2]);
                            figure.HatchHandle = data[3];

                            this.Figures.Add(data[1], figure);

                            ICollection<string> elevationCollection = reader.ReadLine().Split(';').ToList();

                            foreach (var elevationItem in elevationCollection)
                            {
                                this.Figures[data[1]].Elevations.Add(elevationItem);
                            }

                            break;

                        case "ELEVATION":
                            if (this.ContainsElevation(data[1]))
                            {
                                continue;
                            }

                            Elevation elevation = new Elevation();

                            string[] attributes = reader.ReadLine().Split(' ');

                            elevation.ExistingElevation = double.Parse(attributes[0]);
                            elevation.DesignElevation = double.Parse(attributes[1]);
                            elevation.Ground = double.Parse(attributes[2]);
                            elevation.Position.Northing = double.Parse(attributes[3]);
                            elevation.Position.Easting = double.Parse(attributes[4]);
                            elevation.ZeroPoint = bool.Parse(attributes[5]);

                            this.Elevations.Add(data[1], elevation);

                            break;
                    }
                }
            }
        }

        public FigureData GetFigureData(string handle)
        {
            var figure = this.Figures[handle];
            FigureData data = new FigureData();

            double elevationSum = 0;
            ICollection<IPoint> points = new List<IPoint>();
            int positiveElevationsCount = 0;
            int negativeElevationsCount = 0;
            int zeroPointsCount = 0;

            foreach (var elevation in figure.Elevations)
            {
                if (!this.Elevations.ContainsKey(elevation))
                {
                    data.IsDestroyed = true;

                    break;
                }

                elevationSum += this.Elevations[elevation].Value;

                points.Add(this.Elevations[elevation].Position);

                if (this.Elevations[elevation].Value > 0)
                {
                    positiveElevationsCount++;
                }

                if (this.Elevations[elevation].Value < 0)
                {
                    negativeElevationsCount++;
                }

                if (this.Elevations[elevation].ZeroPoint)
                {
                    zeroPointsCount++;
                }
            }

            if (!data.IsDestroyed)
            {
                data.SingleFigure = (positiveElevationsCount > 0 && negativeElevationsCount > 0) || zeroPointsCount > 0 ? false : true;
                data.Area = GeometryHelper.GetArea(points);
                data.AverageElevation = elevationSum / figure.Elevations.Count;
            }


            return data;
        }

        public bool ContainsFigure(string handle)
        {
            return this.Figures.ContainsKey(handle);
        }

        public bool ContainsElevation(string handle)
        {
            return this.Elevations.ContainsKey(handle);
        }
    }
}