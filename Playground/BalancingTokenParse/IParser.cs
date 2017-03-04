using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse
{
    interface IParser
    {
        void Parse(IReadOnlyList<Token> tokens, IParserListener listener);
    }

    interface IParserListener
    {
        void OnSymbolParsed(Symbol symbol, Rule rule);
    }
}
