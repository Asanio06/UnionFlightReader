using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightDataClass;
using UnionFlight.FlightData;
using UnionFlightXplaneReader;

namespace UnionFlight
{
    public class UnionFlight : IUnionFlight
    {
        private List<IFlightReader> _flightReaders = new List<IFlightReader>() {XPReader.Instance};

        private IFlightReader? _flightReader = null;

        public IFlight? flight => _flightReader != null ? _flightReader.flight : null;
        public IAircraft? aircraft => _flightReader != null ? _flightReader.aircraft : null;
        public ISim? simulator => _flightReader != null ? _flightReader.simulator : null;


        public void run()
        {
            foreach (var flightReader in _flightReaders)
            {
                if (flightReader.IsSimLaunched())
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


        public bool IsLaunched()
        {
            if (_flightReader == null)
            {
                foreach (var flightReader in _flightReaders)
                {
                    if (flightReader.IsSimLaunched())
                    {
                        _flightReader = flightReader;
                        flightReader.run();
                        return true;
                    }
                }

                _flightReader = null;
                return false;
            }

            if (!_flightReader.IsSimLaunched())
            {
                _flightReader = null;
                return false;
            }

            return true;
        }

        private UnionFlight(){}

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