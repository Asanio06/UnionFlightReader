using System;
using System.Collections.Generic;
using System.Text;
using UnionFlightXplaneReader.DataReader;

namespace UnionFlightXplaneReader
{
    public class Aircraft
    {
        private static AircraftReader aircraftReader = AircraftReader.Instance;

        public float? IndicatedAirspeed => aircraftReader.IndicatedAirspeedReader.Value ;

        public float? Groundspeed => aircraftReader.Groundspeed.Value;

        public float? TrueAirspeed => aircraftReader.TrueAirSpeed.Value;
        public double? Altitude => aircraftReader.Altitude.Value;
        public double? HeightAgl => aircraftReader.HeightAgl.Value;
        public double? Longitude => aircraftReader.Longitude.Value;
        public double? Latitude => aircraftReader.Latitude.Value;
        public float? VerticalSpeed => aircraftReader.VerticalSpeed.Value;
        public float? Heading => aircraftReader.Heading.Value;

        public string TailNumber
        {
            get => aircraftReader.TailNumberReader.Value;
        }

        public string? Name
        {
            get => aircraftReader.NameReader.Value;
        }



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