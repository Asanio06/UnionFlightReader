using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnionFlight.FlightData;
using UnionFlightXplaneReader;

namespace UnionFlight
{
    public class UnionFlight
    {
        private List<IFlightReader> _flightReaders = new List<IFlightReader>() {XPReader.Instance};

        private IFlightReader? _flightReader = null;

        public IFlight? flight => _flightReader != null ? _flightReader.flight : null;


        public void run()
        {
            foreach (var flightReader in _flightReaders)
            {
                if (flightReader.isLaunched())
                {
                    _flightReader = flightReader;
                    flightReader.run();
                    break;
                }
            }
        }

        // TODO: Gérer les exceptions null
        public void stop()
        {
            _flightReader.stop();
        }

        public string GetSimName()
        {
            return _flightReader.GetSimName();
        }


        public bool IsLaunched()
        {
            if (_flightReader == null)
            {
                foreach (var flightReader in _flightReaders)
                {
                    if (flightReader.isLaunched())
                    {
                        _flightReader = flightReader;
                        flightReader.run();
                        return true;
                    }
                }

                _flightReader = null;
                return false;
            }

            if (!_flightReader.isLaunched())
            {
                _flightReader = null;
                return false;
            }

            return true;
        }


        public static UnionFlight Instance
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

            internal static readonly UnionFlight instance = new();
        }
    }
}