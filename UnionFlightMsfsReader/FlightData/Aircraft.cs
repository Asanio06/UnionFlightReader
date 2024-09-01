using UnionFlight.FlightData;

namespace UnionFlightMsfsReader
{
    internal class Aircraft : IAircraft
    {


        private AircraftReader _aircraftReader = AircraftReader.Instance;



        public float? Groundspeed => _aircraftReader.Groundspeed.Value;

        public float? IndicatedAirspeed => _aircraftReader.IndicatedAirspeed.Value;

        public float? TrueAirspeed => _aircraftReader.TrueAirspeed.Value;

        public double? Altitude => AircraftReader.Instance.Altitude.Value;

        public double? HeightAgl => throw new NotImplementedException();

        public double? Longitude => throw new NotImplementedException();

        public double? Latitude => throw new NotImplementedException();

        public float? VerticalSpeed => throw new NotImplementedException();

        public float? Heading => throw new NotImplementedException();

        public string? Name => AircraftReader.Instance.Name.Value;

        public string? TailNumber => AircraftReader.Instance.TailNumber.Value;


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

            internal static readonly Aircraft instance = new();
        }
    }
}
