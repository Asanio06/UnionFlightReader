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

            while (true)
            {
                Console.WriteLine(
                    $"TailNumberReader = {aircraft.TailNumber}");
                Console.WriteLine(
                    $"IndicatedAirspeedReader = {aircraft.IndicatedAirspeed}");
               Console.WriteLine(
                    $"Name = {aircraft.Name}");

                Thread.Sleep(2000);
            }
        }
    }
}