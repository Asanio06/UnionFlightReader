using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnionFlight.FlightData
{
    public interface IFlight
    {

        public IAircraft aircraft { get; }
    }
}
