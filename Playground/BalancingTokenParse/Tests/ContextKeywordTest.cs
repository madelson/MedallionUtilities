using Medallion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Playground.BalancingTokenParse.Tests
{
    public class ContextKeywordTest
    {
        private static readonly Token ID = new Token("ID"),
            ASYNC = new Token("async"),
            AWAIT = new Token("await"),
            OPEN_BRACE = new Token("{"),
            CLOSE_BRACE = new Token("}"),
            OPEN_BRACKET = new Token("["),
            CLOSE_BRACKET = new Token("]"),
            OPEN_PAREN = new Token("("),
            CLOSE_PAREN = new Token(")"),
            SEMICOLON = new Token(";"),
            COMMA = new Token(","),
            LT = new Token("<"),
            GT = new Token(">");

        private static readonly NonTerminal Exp = new NonTerminal("Exp"),
            ExpList = new NonTerminal("List<Exp>"),
            Stmt = new NonTerminal("Stmt"),
            StmtList = new NonTerminal("List<Stmt>"),
            Start = new NonTerminal("Start"),
            Method = new NonTerminal("Method"),
            Identifier = new NonTerminal("Id");

        private readonly ITestOutputHelper output;

        public ContextKeywordTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Tests a grammar like C#'s async/await. We have the async keyword which enables await to appear
        /// within the following method block. Otherwise, await is simply an identifier
        /// </summary>
        [Fact]
        public void TestAsyncAwait()
        {
            var beginMethod = new NonTerminal("BeginMethod");
            var endMethod = new NonTerminal("EndMethod");
            var asyncOption = new NonTerminal($"Option<{ASYNC.Name}>");

            var rules = new[]
            {
                new Rule(Start, Method),
                new Rule(Method, beginMethod, asyncOption, ID, OPEN_BRACE, StmtList, CLOSE_BRACE, endMethod),
                new Rule(beginMethod, new Symbol[0], action: new RuleAction("asyncMethod", RuleActionKind.Push)),
                new Rule(endMethod, new Symbol[0], action: new RuleAction("asyncMethod", RuleActionKind.Pop)),

                new Rule(asyncOption, new Symbol[] { ASYNC }, action: new RuleAction("asyncMethod", RuleActionKind.Set)),
                new Rule(asyncOption),

                new Rule(StmtList, Stmt, StmtList),
                new Rule(StmtList),

                new Rule(Stmt, Exp, SEMICOLON),

                new Rule(Exp, Identifier),
                new Rule(Exp, new Symbol[] { AWAIT, Exp }, requiredParserVariable: "asyncMethod"),

                new Rule(Identifier, ID),
                new Rule(Identifier, AWAIT),
            };

            var nodes = ParserBuilder.CreateParser(rules);
            var parser = new ParserNodeParser(nodes, Start, this.output.WriteLine);

            var listener1 = new TreeListener();
            parser.Parse(new[] { ASYNC, ID, OPEN_BRACE, AWAIT, ID, SEMICOLON, AWAIT, SEMICOLON, CLOSE_BRACE }, listener1);
            this.output.WriteLine(listener1.Root.Flatten().ToString());
            listener1.Root.Flatten().ToString().ShouldEqual("Start(Method(BeginMethod(), Option<async>(async), ID, {, List<Stmt>(Stmt(Exp(await, Exp(Id(ID))), ;), Stmt(Exp(Id(await)), ;)), }, EndMethod()))");
            
            var ex = Assert.Throws<InvalidOperationException>(() => parser.Parse(new[] { ID, OPEN_BRACE, AWAIT, ID, SEMICOLON, AWAIT, SEMICOLON, CLOSE_BRACE }, new TreeListener()));
            this.output.WriteLine(ex.ToString());
            ex.Message.ShouldEqual("Cannot parse Exp -> await Exp { REQUIRE asyncMethod } without variable asyncMethod");
        }

        /// <summary>
        /// Tests a grammar like C#'s async/await. We have the async keyword which enables await to appear
        /// within the following method block. Otherwise, await is simply an identifier
        /// </summary>
        [Fact]
        public void TestAsyncAwaitWithAmbiguity()
        {
            var beginMethod = new NonTerminal("BeginMethod");
            var endMethod = new NonTerminal("EndMethod");
            var asyncOption = new NonTerminal($"Option<{ASYNC.Name}>");
            var call = new NonTerminal("Call");
            var arguments = new NonTerminal("Arguments");
            var argumentsTail = new NonTerminal("ArgumentsTail");

            var awaitRule = new Rule(Exp, new Symbol[] { AWAIT, Exp }, requiredParserVariable: "asyncMethod");
            var rules = new[]
            {
                new Rule(Start, Method),
                new Rule(Method, beginMethod, asyncOption, ID, OPEN_BRACE, StmtList, CLOSE_BRACE, endMethod),
                new Rule(beginMethod, new Symbol[0], action: new RuleAction("asyncMethod", RuleActionKind.Push)),
                new Rule(endMethod, new Symbol[0], action: new RuleAction("asyncMethod", RuleActionKind.Pop)),

                new Rule(asyncOption, new Symbol[] { ASYNC }, action: new RuleAction("asyncMethod", RuleActionKind.Set)),
                new Rule(asyncOption),

                new Rule(StmtList, Stmt, StmtList),
                new Rule(StmtList),

                new Rule(Stmt, Exp, SEMICOLON),

                new Rule(Exp, Identifier),
                new Rule(Exp, OPEN_PAREN, Exp, CLOSE_PAREN),
                new Rule(Exp, call),
                awaitRule,

                new Rule(call, Identifier, OPEN_PAREN, arguments, CLOSE_PAREN),
                
                new Rule(arguments),
                new Rule(arguments, Exp, argumentsTail),
                new Rule(argumentsTail),
                new Rule(argumentsTail, COMMA, Exp, argumentsTail),

                new Rule(Identifier, ID),
                new Rule(Identifier, AWAIT),
            };

            var nodes = ParserBuilder.CreateParser(rules);
            var parser = new ParserNodeParser(nodes, Start, this.output.WriteLine);

            var listener1 = new TreeListener();
            parser.Parse(new[] { ASYNC, ID, OPEN_BRACE, AWAIT, OPEN_PAREN, ID, CLOSE_PAREN, SEMICOLON, CLOSE_BRACE }, listener1);
            this.output.WriteLine(listener1.Root.Flatten().ToString());
            listener1.Root.Flatten().ToString().ShouldEqual("Start(Method(BeginMethod(), Option<async>(async), ID, {, List<Stmt>(Stmt(Exp(await, Exp((, Exp(Id(ID)), ))), ;)), }, EndMethod()))");

            var listener2 = new TreeListener();
            parser.Parse(new[] { ID, OPEN_BRACE, AWAIT, OPEN_PAREN, ID, CLOSE_PAREN, SEMICOLON, CLOSE_BRACE }, listener2);
            this.output.WriteLine(listener2.Root.Flatten().ToString());
            listener2.Root.Flatten().ToString().ShouldEqual("Start(Method(BeginMethod(), Option<async>(), ID, {, List<Stmt>(Stmt(Exp(Call(Id(await), (, Arguments(Exp(Id(ID)), ArgumentsTail()), ))), ;)), }, EndMethod()))");
        }
    }
}
