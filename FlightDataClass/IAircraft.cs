using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnionFlight.FlightData
{
    public interface IAircraft
    {
        public float? Groundspeed { get; }

        public float? IndicatedAirspeed { get; }

        public float? TrueAirspeed { get; }
        public double? Altitude { get; }
        public double? HeightAgl { get; }
        public double? Longitude { get; }
        public double? Latitude { get; }
        public float? VerticalSpeed { get; }
        public float? Heading { get; }

        public string TailNumber { get; }

        public string? Name { get; }
    }
}