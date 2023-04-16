using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnionFlightXplaneReader;

namespace ConsoleExemple
{
    class Program
    {
        static XPReader xpReader = XPReader.Instance;

        static Aircraft aircraft = Aircraft.Instance;


        static void Main(string[] args)
        {
            xpReader.run();

            Func<dynamic, string> format = x =>
            {
                if (x == null) return "0";
                return string.Format("{0:0.##}", x);
            };

            while (true)
            {
                Console.WriteLine(
                    $"-----------------------------------------------------------");

                Console.WriteLine(
                    $"TailNumberReader = {aircraft.TailNumber}");

                Console.WriteLine(
                    $"IndicatedAirspeedReader = {format(aircraft.IndicatedAirspeed)}");

                Console.WriteLine(
                    $"TrueAirspeed = {format(aircraft.TrueAirspeed)}");

                Console.WriteLine(
                    $"Groundspeed = {format(aircraft.Groundspeed)}");

                Console.WriteLine(
                    $"Name = {aircraft.Name}");


                Console.WriteLine(
                    $"Altitude = {format(aircraft.Altitude)}");


                Console.WriteLine(
                    $"HeightAgl = {format(aircraft.HeightAgl)}");

                Console.WriteLine(
                    $"VerticalSpeed = {format(aircraft.VerticalSpeed)}");

                Console.WriteLine(
                    $"Heading = {format(aircraft.Heading)}");

                Console.WriteLine(
                    $"Latitude = {format(aircraft.Latitude)}");

                Console.WriteLine(
                    $"Longitude = {format(aircraft.Longitude)}");

                Console.WriteLine(
                    $"-----------------------------------------------------------");
                Thread.Sleep(2000);
            }
        }
    }
}