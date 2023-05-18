using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightDataClass
{
    public interface ISim
    {

        public bool? IsFlightLaunched { get; }
        public string? SimulatorName { get; }

    }
}
