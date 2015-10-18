using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Funshot
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(resolveAssembly);
        }

        private static Assembly resolveAssembly(object sender, ResolveEventArgs args)
        {
            var parentAssembly = Assembly.GetExecutingAssembly();
            var finalname = args.Name.Substring(0, args.Name.IndexOf(',')) + ".dll";
            var ResourcesList = parentAssembly.GetManifestResourceNames();
            string OurResourceName = null;
            for (int i = 0; i <= ResourcesList.Length - 1; i++)
            {
                var name = ResourcesList[i];
                if (name.EndsWith(finalname))
                {
                    OurResourceName = name;
                    break;
                }
            }
            if (!string.IsNullOrWhiteSpace(OurResourceName))
            {
                using (Stream stream = parentAssembly.GetManifestResourceStream(OurResourceName))
                {
                    byte[] block = new byte[stream.Length];
                    stream.Read(block, 0, block.Length);
                    return Assembly.Load(block);
                }
            }
            else
            {
                return null;
            }
        }
    }
}