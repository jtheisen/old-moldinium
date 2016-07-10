using System;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var tests = new UnitTests.Lists.LiveListTest();

                tests.Wheres();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ex.: " + ex);
            }

            Console.ReadKey();
        }
    }
}
