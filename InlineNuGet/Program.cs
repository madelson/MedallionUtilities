using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Tools
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Creating inline NuGet package...");

                var nuspec = args.Length == 0
                    ? Directory.GetFiles(Environment.CurrentDirectory, "*.nuspec").FirstOrDefault()
                    : args[0];

                if (nuspec == null)
                {
                    throw new FileNotFoundException("No nuspec file found");
                }
                Console.WriteLine($"Using nuspec {nuspec}");

                var projectFile = Directory.GetFiles(Path.GetDirectoryName(nuspec), "*.*proj").Single();
                Console.WriteLine($"Using project file {projectFile}");

                var packed = InlineNuGetPackageCreator.Create(projectFilePath: projectFile, nuspec: nuspec, outputDirectory: Environment.CurrentDirectory);
                Console.WriteLine($"Created {string.Join(", ", packed)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("==== AN ERROR OCCURRED ====");
                Console.WriteLine(ex);
                Console.WriteLine("===========================");
            }
            finally
            {
                Console.Out.Flush();
                Console.Error.Flush();
            }
        }
    }
}
