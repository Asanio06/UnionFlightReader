using System;
using System.Collections.Generic;
using System.Text;

namespace UnionFlightXPReader
{
    class AircraftReader
    {
        public DataReader AirspeedDataReader = new FloatDataReader();
        public DataReader TailNumberDataReader = new StringDataReader(40);
        public DataReader AircraftNameDataReader = new StringDataReader(250);


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