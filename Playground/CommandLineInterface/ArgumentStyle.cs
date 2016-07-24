using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    [Flags]
    public enum NamedArgumentStyles
    {
        CaseInsensitive = 1 << 0,

        DashPrefixedNames = 1 << 1,
        DashPrefixedShortNames = 1 << 2,
        DoubleDashPrefixedNames = 1 << 3,
        SlashPrefixedNames = 1 << 4,

        TokenSeparatedValues = 1 << 5,
        ColonSeparatedValues = 1 << 6,

        AllowCombinedFlags = 1 << 7,
        DoubleDashEndsNamedArguments = 1 << 8,

        /// <summary>
        /// 
        /// </summary>
        // based on http://pubs.opengroup.org/onlinepubs/9699919799/basedefs/V1_chap12.html
        Unix = DashPrefixedShortNames | DoubleDashPrefixedNames | TokenSeparatedValues | AllowCombinedFlags | DoubleDashEndsNamedArguments,

        /// <summary>
        /// 
        /// </summary>
        // based on https://technet.microsoft.com/en-us/library/ee156811.aspx
        Powershell = CaseInsensitive | DashPrefixedNames | DashPrefixedShortNames | TokenSeparatedValues | ColonSeparatedValues,

        /// <summary>
        /// 
        /// </summary>
        // based on the dir command
        Windows = CaseInsensitive | SlashPrefixedNames | ColonSeparatedValues,
    }
}
