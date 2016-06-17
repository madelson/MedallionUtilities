using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class ArgumentStyle
    {

    }

    public sealed class EnumParseBehavior
    {
        public bool IgnoreCase { get; }
        public bool AllowNames { get; }
        public bool AllowNumbers { get; }
        public bool RequireDefined { get; } 
    }
}
