using Medallion;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            var goesTo = new Token("=>");
            var var = new Token("var");
            var assign = new Token("=");

            var start = new NonTerminal("Start");
            var stmt = new NonTerminal("Stmt");
            var stmtList = new NonTerminal("List<Stmt>");
            var exp = new NonTerminal("Exp");
            var ident = new NonTerminal("Ident");
            var tuple = new NonTerminal("Tuple");
            var tupleMemberBinding = new NonTerminal("TupleMemberBinding");
            var tupleMemberBindingList = new NonTerminal("List<TupleMemberBinding>");
            var expBlock = new NonTerminal("ExpBlock");
            var lambda = new NonTerminal("Lambda");
            var lambdaParameters = new NonTerminal("LambdaArgs");
            var lambdaParameterList = new NonTerminal("List<LambdaArg>");
            var assignment = new NonTerminal("Assignment");
            var call = new NonTerminal("Call");
            var argList = new NonTerminal("List<Arg>");

            var rules = new Rule[]
            {
                new Rule(start, stmtList),

                new Rule(stmtList, stmt, stmtList),
                new Rule(stmtList),

                new Rule(stmt, exp, semi),
                new Rule(stmt, @return, exp, semi),
                new Rule(stmt, assignment),

                new Rule(exp, ident),
                new Rule(exp, num),
                new Rule(exp, openParen, exp, closeParen),
                new Rule(exp, exp, times, exp),
                new Rule(exp, exp, plus, exp),
                new Rule(exp, tuple),
                new Rule(exp, expBlock),
                new Rule(exp, lambda),
                new Rule(exp, call),

                new Rule(ident, id),
                new Rule(ident, var),

                new Rule(tuple, openParen, tupleMemberBindingList, closeParen),

                new Rule(tupleMemberBindingList, tupleMemberBinding, comma, tupleMemberBindingList),
                new Rule(tupleMemberBindingList, tupleMemberBinding),
                new Rule(tupleMemberBindingList),

                new Rule(tupleMemberBinding, ident, colon, exp),

                new Rule(expBlock, openParen, stmt, stmtList, closeParen),
            
                new Rule(lambda, lambdaParameters, goesTo, exp),

                new Rule(lambdaParameters, ident),
                new Rule(lambdaParameters, openParen, lambdaParameterList, closeParen),

                new Rule(lambdaParameterList),
                new Rule(lambdaParameterList, ident, comma, lambdaParameterList),
                new Rule(lambdaParameterList, ident),

                new Rule(assignment, var, ident, assign, exp, semi),

                new Rule(call, exp, openParen, argList, closeParen),

                new Rule(argList),
                new Rule(argList, exp, comma, argList),
                new Rule(argList, exp),
            };

            var rewritten = LeftRecursionRewriter.Rewrite(rules, rightAssociativeRules: ImmutableHashSet<Rule>.Empty);
            var nodes = ParserBuilder.CreateParser(rewritten.Keys);
            var parser = new ParserNodeParser(nodes, start, this.output.WriteLine);

            var listener = new TreeListener(rewritten);
            const string Code = @"
                var a = 2;
                var func = i => (var x = i + a; return x;);
                var t = (z: a, y: func);
                func(77);
            ";
            var tokens = Lex(Code, rules);
            
            Record.Exception(() => parser.Parse(tokens, listener))
                .ShouldEqual(null);
        }

        private static List<Token> Lex(string code, IReadOnlyList<Rule> rules)
        {
            var allTokens = rules.SelectMany(r => r.Symbols)
                .OfType<Token>()
                .Distinct()
                .ToDictionary(t => t.Name);

            var parts = Regex.Split(code, @"\s+");
            var result = new List<Token>();
            foreach (var part in parts)
            {
                var remaining = part;
                while (remaining.Length > 0)
                {
                    var identMatch = Regex.Match(remaining, @"^[^\W\d_]\w*");
                    if (identMatch.Success)
                    {
                        result.Add(allTokens.TryGetValue(identMatch.Value, out var keyword) ? keyword : allTokens["ID"]);
                        remaining = remaining.Substring(startIndex: identMatch.Length);
                    }
                    else
                    {
                        var numMatch = Regex.Match(remaining, @"^\d+");
                        if (numMatch.Success)
                        {
                            result.Add(allTokens["NUM"]);
                            remaining = remaining.Substring(startIndex: numMatch.Length);
                        }
                        else
                        {
                            var token = allTokens.Values.Where(t => remaining.StartsWith(t.Name))
                                .OrderByDescending(t => t.Name.Length)
                                .First();
                            result.Add(token);
                            remaining = remaining.Substring(startIndex: token.Name.Length);
                        }
                    }
                }
            }

            return result;
        }
    }
}
