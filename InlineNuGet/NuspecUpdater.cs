using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Medallion.Tools
{
    public class NuspecUpdater
    {
        public static string RewriteNuspec(string nuspecPath, Project project, string codeFileName)
        {
            var nuspecText = File.ReadAllText(nuspecPath);
            var substituted = ReplaceTokens(nuspecText, project);
            
            var parsed = XDocument.Parse(substituted);
            var ns = parsed.Root.Name.Namespace;

            // update id
            var idElement = parsed.Descendants(ns + "id").Single();
            idElement.SetValue(idElement.Value + ".Inline");

            // update dev dependency
            XElement developmentDependencyElement;
            var existingDevelopmentDependencyElement = parsed.Descendants(ns + "developmentDependency").SingleOrDefault();
            if (existingDevelopmentDependencyElement != null)
            {
                developmentDependencyElement = existingDevelopmentDependencyElement;
            }
            else
            {
                developmentDependencyElement = new XElement(ns + "developmentDependency");
                parsed.Descendants(ns + "metadata").Single().Add(developmentDependencyElement);
            }
            developmentDependencyElement.SetValue("true");

            // update files
            XElement filesElement;
            var existingFilesElement = parsed.Element(ns + "package").Element(ns + "files");
            if (existingFilesElement != null)
            {
                var docXmlElement = existingFilesElement.Elements(ns + "file")
                    .FirstOrDefault(f => StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(f.Attribute(ns + "src").Value), Path.GetFileNameWithoutExtension(project.FilePath) + ".xml"));
                if (docXmlElement != null)
                {
                    docXmlElement.Remove();
                    Console.WriteLine($"Removed reference to {docXmlElement.Attribute(ns + "src").Value}");
                }

                filesElement = existingFilesElement;
            }
            else
            {
                filesElement = new XElement(ns + "files");
                parsed.Element(ns + "package").Add(filesElement);
            }

            var codeFileElement = XElement.Parse($"<file src=\"{WebUtility.HtmlEncode(Path.GetFileName(codeFileName))}\" target=\"content\" />");
            filesElement.Add(codeFileElement);

            var result = parsed.ToString();
            return result;
        }

        private static string ReplaceTokens(string nuspecText, Project project)
        {
            var compilation = project.GetCompilationAsync().Result;

            var result = nuspecText.LazyReplace("$id$", () => WebUtility.HtmlEncode(compilation.Assembly.Name))
                .LazyReplace(
                    "$version$", 
                    () => WebUtility.HtmlEncode(
                        (string)(
                            FindAssemblyAttribute(compilation, typeof(AssemblyInformationalVersionAttribute))
                            ?? FindAssemblyAttribute(compilation, typeof(AssemblyVersionAttribute))
                        )
                        .ConstructorArguments
                        .Single().Value
                    )
                )
                .LazyReplace("$author$", () => WebUtility.HtmlEncode((string)FindAssemblyAttribute(compilation, typeof(AssemblyCompanyAttribute)).ConstructorArguments.Single().Value))
                .LazyReplace("$description$", () => WebUtility.HtmlEncode((string)FindAssemblyAttribute(compilation, typeof(AssemblyDescriptionAttribute)).ConstructorArguments.Single().Value));

            return result;
        }  

        private static AttributeData FindAssemblyAttribute(Compilation compilation, Type type)
        {
            var result = compilation.Assembly.GetAttributes()
                .Single(a => a.AttributeClass.ContainingNamespace + "." + a.AttributeClass.Name == type.ToString());
            return result;
        }
    }

    internal static class StringHelpers
    {
        public static string LazyReplace(this string text, string toReplace, Func<string> replacement)
        {
            return text.Contains(toReplace)
                ? text.Replace(toReplace, replacement())
                : text;
        }
    }
}
