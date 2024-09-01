using FlightDataClass;

namespace UnionFlightMsfsReader.FlightData
{
    internal class Simulator : ISim
    {



        public bool? IsFlightLaunched => throw new NotImplementedException();

        public bool? IsSimPaused => throw new NotImplementedException();

        public string? SimulatorName => "Microsoft Flight Simulator";


        private Simulator()
        {
        }

        public static Simulator Instance
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

            internal static readonly Simulator instance = new();
        }
    }
}
