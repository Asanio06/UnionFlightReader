namespace UnionFlightMsfsReader
{
    internal class AircraftReader
    {

        public FloatDataReader Altitude = new();
        public StringDataReader Name = new();


        private AircraftReader()
        {
        }

        public static AircraftReader Instance
        {
            get { return Nested.instance; }
        }

        private class Nested
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static Nested()
            {
            }

            internal static readonly AircraftReader instance = new AircraftReader();
        }
    }
}
