using System;
using System.Threading;

namespace ConsoleExemple
{
    class Program
    {
        static UnionFlight.UnionFlight unionFlight = UnionFlight.UnionFlight.Instance;

        static void Main(string[] args)
        {

            Func<dynamic, string> format = x =>
            {
                if (x == null) return "0";
                return string.Format("{0:0.##}", x);
            };

            while (true)
            {
                if (unionFlight.IsLaunched())
                {
                    //var simulator = unionFlight.simulator;
                    var aircraft = unionFlight.aircraft;

                    Console.WriteLine(
                        $"-----------------------------------------------------------");

                    Console.WriteLine($"Altitude = {format(aircraft.Altitude)}");

                    //Console.WriteLine($"IsFlightLaunched = {simulator.IsFlightLaunched}");
                    //Console.WriteLine($"SimulatorName = {simulator.SimulatorName}");
                    //Console.WriteLine($"IsPaused = {simulator.IsSimPaused}");

                    Console.WriteLine(
                        $"TailNumber = {aircraft.TailNumber}");

                    Console.WriteLine(
                        $"Name = {aircraft.Name}");

                    Console.WriteLine(
                        $"IndicatedAirspeed = {format(aircraft.IndicatedAirspeed)}");

                    Console.WriteLine(
                        $"TrueAirspeed = {format(aircraft.TrueAirspeed)}");

                    Console.WriteLine(
                        $"Groundspeed = {format(aircraft.Groundspeed)}");

                    Console.WriteLine(
                        $"VerticalSpeed = {format(aircraft.VerticalSpeed)}");

                    Console.WriteLine(
                        $"Heading = {format(aircraft.Heading)}");

                    //
                    //

                    //
                    //
                    //                    Console.WriteLine(
                    //                        $"HeightAgl = {format(aircraft.HeightAgl)}");
                    //

                    //

                    //
                    Console.WriteLine(
                        $"Latitude = {format(aircraft.Latitude)}");

                    Console.WriteLine(
                        $"Longitude = {format(aircraft.Longitude)}");

                    Console.WriteLine(
                        $"-----------------------------------------------------------");
                }
                else
                {
                    Console.WriteLine("Not Launched");
                }

                Thread.Sleep(1000);
            }
        }
    }
}