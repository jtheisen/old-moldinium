using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace SampleApp
{
    public partial class App : Application
    {
        public static T GetResourceObject<T>(String fileName)
        {
            return JsonConvert.DeserializeObject<T>(GetResourceString(fileName));
        }

        public static String GetResourceString(String fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            var names = assembly.GetManifestResourceNames();

            using (var stream = assembly.GetManifestResourceStream($"{nameof(SampleApp)}.{fileName}"))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }
    }
}
