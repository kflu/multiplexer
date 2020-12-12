namespace Multiplexer
{
    using System;

    static class Logger
    {
        public static void Log(string msg)
        {
            Console.Error.WriteLine(msg);
        }
    }
}