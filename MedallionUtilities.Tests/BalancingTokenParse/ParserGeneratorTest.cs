using Playground.BalancingTokenParse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Medallion.BalancingTokenParse
{
    public class ParserGeneratorTest
    {
        private static readonly Token ID = new Token("ID"),
            OPEN_BRACKET = new Token("["),
            CLOSE_BRACKET = new Token("]"),
            SEMICOLON = new Token(";"),
            COMMA = new Token(",");

        private static readonly NonTerminal Exp = new NonTerminal("Exp"),
            ExpList = new NonTerminal("List<Exp>"),
            Stmt = new NonTerminal("Stmt"),
            StmtList = new NonTerminal("List<Stmt>"),
            Start = new NonTerminal("Start");

        private readonly ITestOutputHelper output;

        public ParserGeneratorTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void TestLL1()
        {
            var rules = new[]
            {
                new Rule(Start, StmtList),
                new Rule(StmtList),
                new Rule(StmtList, Stmt, StmtList),
                new Rule(Stmt, Exp, SEMICOLON),
                new Rule(Exp, ID),
                new Rule(Exp, OPEN_BRACKET, ExpList, CLOSE_BRACKET),
                new Rule(ExpList),
                new Rule(ExpList, Exp, ExpList)
            };
            var parser = new ParserGenerator(rules).Create();

            var listener = new TreeListener();
            parser.Parse(new[] { ID, SEMICOLON, OPEN_BRACKET, ID, OPEN_BRACKET, ID, ID, CLOSE_BRACKET, CLOSE_BRACKET, SEMICOLON }, listener);

            this.output.WriteLine(listener.Root.Flatten().ToString());
            listener.Root.Flatten().ToString()
                .ShouldEqual("Start(List<Stmt>(Stmt(Exp(ID), ;), Stmt(Exp([, List<Exp>(Exp(ID), Exp([, List<Exp>(Exp(ID), Exp(ID)), ])), ]), ;)))");
        }

        [Fact]
        public void TestExpressionVsStatementListConflict()
        {
            var rules = new[]
            {
                new Rule(Start, Stmt),
                new Rule(Stmt, Exp, SEMICOLON),
                new Rule(Exp, ID),
                new Rule(Exp, OPEN_BRACKET, ExpList, CLOSE_BRACKET),
                new Rule(Exp, OPEN_BRACKET, Stmt, StmtList, CLOSE_BRACKET),
                new Rule(ExpList),
                new Rule(ExpList, Exp, ExpList),
                new Rule(StmtList),
                new Rule(StmtList, Stmt, StmtList)
            };

            ParserBuilder.CreateParser(rules);
            //var parser = new ParserGenerator(rules).Create();
        }
    }
}
