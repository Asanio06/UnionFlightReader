using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightDataClass;
using UnionFlight.FlightData;

namespace UnionFlight
{
    internal interface IUnionFlight
    {
        public IFlight? flight { get; }
        public IAircraft? aircraft { get; }
        public ISim? simulator { get; }


        public void run();
        public void stop();
        public bool IsLaunched();
    }
}