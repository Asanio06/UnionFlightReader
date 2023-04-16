using System;
using System.Collections.Generic;
using System.Text;

namespace UnionFlightXplaneReader.DataReader
{
    internal class AircraftReader
    {
        public DataReader IndicatedAirspeedReader = new FloatDataReader();
        public DataReader Groundspeed = new FloatDataReader(Utils.getKnotsFromMeterPerSeconds);
        public DataReader TrueAirSpeed = new FloatDataReader(Utils.getKnotsFromMeterPerSeconds);
        public DataReader Heading = new FloatDataReader();
        public DataReader Altitude = new DoubleDataReader(Utils.getFeetFromMeter); // Based on sea level
        public DataReader HeightAgl = new DoubleDataReader(Utils.getFeetFromMeter);
        public DataReader VerticalSpeed = new FloatDataReader();
        public DataReader Longitude = new DoubleDataReader();
        public DataReader Latitude = new DoubleDataReader();
        public DataReader TailNumberReader = new StringDataReader(40);
        public DataReader NameReader = new StringDataReader(250);


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