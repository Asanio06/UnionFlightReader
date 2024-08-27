using FlightDataClass;

namespace UnionFlightMsfsReader.FlightData
{
    internal class Simulator : ISim
    {



        public bool? IsFlightLaunched => throw new NotImplementedException();

        public bool? IsSimPaused => throw new NotImplementedException();

        public string? SimulatorName => "Microsoft Flight Simulator";
    }
}
