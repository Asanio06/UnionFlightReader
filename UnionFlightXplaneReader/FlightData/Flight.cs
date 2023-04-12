using System;
using System.Collections.Generic;
using System.Text;

namespace UnionFlightXplaneReader
{
    public class Flight
    {


        public Aircraft aircraft = Aircraft.Instance;
        


        private Flight()
        {
        }

        public static Flight Instance
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

            internal static readonly Flight instance = new Flight();
        }
    }
}
