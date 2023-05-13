using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnionFlight.FlightData;

namespace UnionFlight
{
    public interface IFlightReader
    {
        public IFlight flight { get; }

        public string GetSimName();

        public bool isLaunched();

        public void run();

        public void stop();
    }
}