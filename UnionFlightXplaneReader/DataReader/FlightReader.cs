using System;
using System.Collections.Generic;
using System.Text;

namespace UnionFlightXplaneReader.DataReader
{
    internal class FlightReader
    {

        public AircraftReader aircraftReader = AircraftReader.Instance;


        private FlightReader()
        {
        }

        public static FlightReader Instance
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

            internal static readonly FlightReader instance = new FlightReader();
        }
    }
}