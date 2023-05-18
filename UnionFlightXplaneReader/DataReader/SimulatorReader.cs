using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnionFlightXplaneReader.DataReader
{
    internal class SimulatorReader
    {
        public DataReader XplaneInternalVersion = new IntDataReader();
        public DataReader XplaneSimSpeed = new FloatDataReader();


        private SimulatorReader()
        {
        }

        public static SimulatorReader Instance
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

            internal static readonly SimulatorReader instance = new SimulatorReader();
        }
    }
}