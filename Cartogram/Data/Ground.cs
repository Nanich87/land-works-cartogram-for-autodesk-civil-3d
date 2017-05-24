namespace Cartogram.Data
{
    internal class Ground
    {
        private string name;
        private double height;

        public Ground(string name, double height)
        {
            this.Name = name;
            this.Height = height;
        }

        public string Name
        {
            get
            {
                return this.name;
            }

            set
            {
                this.name = value;
            }
        }

        public double Height
        {
            get
            {
                return this.height;
            }

            set
            {
                this.height = value;
            }
        }
    }
}