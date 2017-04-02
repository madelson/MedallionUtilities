using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;

namespace Playground.ApiTracker
{
    public class ApiTracker
    {
        private readonly AssemblyDefinition assembly;

        public ApiTracker(AssemblyDefinition assembly)
        {
            this.assembly = assembly;

            //System.Reflection.MemberTypes.
            //this.assembly.MainModule.Types[0]
        }

        
    }
}
