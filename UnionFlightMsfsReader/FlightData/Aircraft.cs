﻿using UnionFlight.FlightData;

namespace UnionFlightMsfsReader
{
    internal class Aircraft : IAircraft
    {

        private AircraftReader _aircraftReader = AircraftReader.Instance;

        public float? Groundspeed => _aircraftReader.Groundspeed.Value;

        public float? IndicatedAirspeed => _aircraftReader.IndicatedAirspeed.Value;

        public float? TrueAirspeed => _aircraftReader.TrueAirspeed.Value;

        public double? Altitude => _aircraftReader.Altitude.Value;

        public double? HeightAgl => throw new NotImplementedException();

        public double? Longitude => _aircraftReader.Longitude.Value;

        public double? Latitude => _aircraftReader.Latitude.Value;

        public float? VerticalSpeed => _aircraftReader.VerticalSpeed.Value;

        public float? Heading => _aircraftReader.Heading.Value;

        public string? Name => _aircraftReader.Name.Value;

        public string? TailNumber => _aircraftReader.TailNumber.Value;


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
