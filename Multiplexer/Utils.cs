namespace Multiplexer
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using CommandLine;

    static class Logger
    {
        public static void Log(string msg)
        {
            Console.Error.WriteLine(msg);
            Console.Error.Flush();
        }
    }
}