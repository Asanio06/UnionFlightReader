using System;
using System.Collections.Generic;
using System.Text;

namespace UnionFlightXPReader.FlightData
{
    class Aircraft
    {
        private Aircraft()
        {
        }

        public static Aircraft Instance
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

            internal static readonly Aircraft instance = new Aircraft();
        }
    }
}