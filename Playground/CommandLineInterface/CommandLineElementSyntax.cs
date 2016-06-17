using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public abstract class CommandLineElementSyntax
    {
        internal CommandLineElementSyntax(string name, string description)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }
            
            this.Name = name;
            this.Description = description;
        }

        public string Name { get; }
        public string Description { get; }
    }
}
