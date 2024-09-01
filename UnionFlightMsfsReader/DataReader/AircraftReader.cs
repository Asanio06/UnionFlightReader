
using Common;

namespace UnionFlightMsfsReader
{
    internal class AircraftReader
    {

        public FloatDataReader Altitude = new();
        public StringDataReader Name = new();
        public StringDataReader TailNumber = new();
        public FloatDataReader TrueAirspeed = new();
        public FloatDataReader IndicatedAirspeed = new();
        public FloatDataReader Groundspeed = new();
        public FloatDataReader VerticalSpeed = new();
        public FloatDataReader Heading = new();
        public DoubleDataReader Latitude = new(Utils.getDegreesFromRadians);
        public DoubleDataReader Longitude = new(Utils.getDegreesFromRadians);


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
