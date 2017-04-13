using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Tools
{
    class NuspecFinder : IDisposable
    {
        private bool isTemporary;

        public NuspecFinder(string specifiedNuspec)
        {
            if (specifiedNuspec != null)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(specifiedNuspec), ".nuspec"))
                {
                    throw new ArgumentException(nameof(specifiedNuspec), $"must be a .nuspec file. Found: {specifiedNuspec}");
                }
                if (!File.Exists(specifiedNuspec)) { throw new FileNotFoundException($"'{specifiedNuspec}'"); }

                this.NuspecPath = specifiedNuspec;
            }
            else
            {
                var located = Directory.GetFiles(Environment.CurrentDirectory, "*.nuspec").SingleOrDefault();
                if (located != null)
                {
                    this.NuspecPath = located;
                }
                else
                {
                    var package = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "bin", "Debug"), "*.nupkg").SingleOrDefault();
                    if (package == null) { throw new FileNotFoundException("Could not find a nuspec or built nupkg"); }

                    Console.WriteLine($"Found built package {package}");
                    using (var archive = new ZipArchive(File.OpenRead(package), ZipArchiveMode.Read))
                    {
                        var nuspecEntry = archive.Entries.Single(e => StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(e.FullName), ".nuspec"));
                        var tempPath = Path.Combine(Environment.CurrentDirectory, $"temp{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.nuspec");
                        using (var entryStream = nuspecEntry.Open())
                        using (var tempPathStream = File.OpenWrite(tempPath))
                        {
                            entryStream.CopyTo(tempPathStream);
                        }

                        this.NuspecPath = tempPath;
                        this.isTemporary = true;
                    }
                }
            }
        }

        public string NuspecPath { get; private set; }

        public void Dispose()
        {
            var tempPath = this.NuspecPath;
            this.NuspecPath = null;
            if (this.isTemporary) { File.Delete(tempPath); }
        }
    }
}
