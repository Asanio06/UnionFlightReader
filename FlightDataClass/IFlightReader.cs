using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightDataClass;
using UnionFlight.FlightData;

namespace UnionFlight
{
    public interface IFlightReader
    {
        public IFlight flight { get; }
        public IAircraft aircraft { get; }
        public ISim simulator { get; }

        public bool IsSimLaunched();

        public void run();

        public void stop();
    }
}