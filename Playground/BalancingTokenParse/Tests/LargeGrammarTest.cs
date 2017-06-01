using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Playground.BalancingTokenParse.Tests
{
    public class LargeGrammarTest
    {
        private readonly ITestOutputHelper output;

        public LargeGrammarTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void TestLargeGrammar()
        {
            var id = new Token("ID");
            var plus = new Token("+");
            var times = new Token("*");
            var num = new Token("NUM");
            var openParen = new Token("(");
            var closeParen = new Token(")");
            var openBrace = new Token("{");
            var closeBrace = new Token("}");
            var semi = new Token(";");
            var comma = new Token(",");
            var colon = new Token(":");
            var @return = new Token("return");

            var start = new NonTerminal("Start");
            var stmt = new NonTerminal("Stmt");
            var stmtList = new NonTerminal("List<Stmt>");
            var exp = new NonTerminal("Exp");
            var ident = new NonTerminal("Ident");
            var tuple = new NonTerminal("Tuple");
            var tupleMemberBinding = new NonTerminal("TupleMemberBinding");
            var tupleMemberBindingList = new NonTerminal("List<TupleMemberBinding>");
            var expBlock = new NonTerminal("ExpBlock");

            var rules = new Rule[]
            {
                new Rule(start, stmtList),

                new Rule(stmtList, stmt, stmtList),
                new Rule(stmtList),

                new Rule(stmt, exp, semi),
                new Rule(stmt, @return, exp, semi),

                new Rule(exp, ident),
                new Rule(exp, num),
                new Rule(exp, openParen, exp, closeParen),
                new Rule(exp, exp, times, exp),
                new Rule(exp, exp, plus, exp),
                new Rule(exp, tuple),
                new Rule(exp, expBlock),

                new Rule(ident, id),

                new Rule(tuple, openParen, tupleMemberBindingList, closeParen),

                new Rule(tupleMemberBindingList, tupleMemberBinding, comma, tupleMemberBindingList),
                new Rule(tupleMemberBindingList, tupleMemberBinding),
                new Rule(tupleMemberBindingList),

                new Rule(tupleMemberBinding, ident, colon, exp),

                new Rule(expBlock, openParen, stmt, stmtList, closeParen),
            };

            var rewritten = LeftRecursionRewriter.Rewrite(rules, rightAssociativeRules: ImmutableHashSet<Rule>.Empty);
            var nodes = ParserBuilder.CreateParser(rewritten.Keys);
        }
    }
}
