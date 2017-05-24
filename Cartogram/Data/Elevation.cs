namespace Cartogram.Data
{
    using Cartogram.Contracts;

    internal class Elevation
    {
        private const string BlockName = "CG_Elevation";

        private readonly IPoint position;
        private double designElevation;
        private double existingElevation;
        private double ground;
        private bool zeroPoint = false;

        public Elevation()
        {
            this.position = new ObservationPoint();
        }

        public Elevation(double existingElevation) : base()
        {
            this.existingElevation = existingElevation;
        }

        public Elevation(double existingElevation, double designElevation) : this(existingElevation)
        {
            this.designElevation = designElevation;
        }

        public Elevation(double existingElevation, double designElevation, double ground) : this(existingElevation, designElevation)
        {
            this.ground = ground;
        }

        public static string ElevationBlockName
        {
            get
            {
                return Elevation.BlockName;
            }
        }

        public bool ZeroPoint
        {
            get
            {
                return this.zeroPoint;
            }

            set
            {
                this.zeroPoint = value;
            }
        }

        public IPoint Position
        {
            get
            {
                return this.position;
            }
        }

        public double Value
        {
            get
            {
                return this.DesignElevation - this.Ground - this.ExistingElevation;
            }
        }

        public double DesignElevation
        {
            get
            {
                return this.designElevation;
            }

            set
            {
                this.designElevation = value;
            }
        }

        public double ExistingElevation
        {
            get
            {
                return this.existingElevation;
            }

            set
            {
                this.existingElevation = value;
            }
        }

        public double Ground
        {
            get
            {
                return this.ground;
            }

            set
            {
                this.ground = value;
            }
        }
    }
}