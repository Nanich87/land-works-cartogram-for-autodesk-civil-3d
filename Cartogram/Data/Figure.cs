namespace Cartogram.Data
{
    using System.Collections.Generic;

    internal class Figure
    {
        private const string BlockName = "CG_Figure";
        private static int nextFigureNumber = 1;

        private readonly ICollection<string> elevations;
        private int number;
        private string hatchHandle;

        public Figure()
        {
            this.elevations = new List<string>();
        }

        public static int NextFigureNumber
        {
            get
            {
                return Figure.nextFigureNumber;
            }

            set
            {
                Figure.nextFigureNumber = value;
            }
        }

        public static string FigureBlockName
        {
            get
            {
                return Figure.BlockName;
            }
        }

        public string HatchHandle
        {
            get
            {
                return this.hatchHandle;
            }

            set
            {
                this.hatchHandle = value;
            }
        }

        public ICollection<string> Elevations
        {
            get
            {
                return this.elevations;
            }
        }

        public int Number
        {
            get
            {
                return this.number;
            }

            set
            {
                this.number = value;
            }
        }
    }
}