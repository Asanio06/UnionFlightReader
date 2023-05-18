using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightDataClass;
using UnionFlightXplaneReader.DataReader;

namespace UnionFlightXplaneReader.FlightData
{
    public class Simulator : ISim
    {
        public string SimulatorName
        {
            get
            {
                var xplaneVersionNumber = SimulatorReader.Instance.XplaneInternalVersion.Value;
                string xplaneVersion = xplaneVersionNumber?.ToString().Substring(0, 2);

                return $"Xplane {xplaneVersion}";
            }
        }

        public bool? IsFlightLaunched
        {
            get
            {
                float? _xplaneSimSpeed = SimulatorReader.Instance.XplaneSimSpeed.Value;

                return _xplaneSimSpeed != null && _xplaneSimSpeed != 0.0f;
            }
        }


        private Simulator()
        {
        }

        public static Simulator Instance
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

            internal static readonly Simulator instance = new();
        }
    }
}