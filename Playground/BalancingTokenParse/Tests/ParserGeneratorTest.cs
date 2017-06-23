using Medallion;
using Playground.BalancingTokenParse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Playground.BalancingTokenParse.Tests
{
    public class ParserGeneratorTest
    {
        private static readonly Token ID = new Token("ID"),
            OPEN_BRACKET = new Token("["),
            CLOSE_BRACKET = new Token("]"),
            OPEN_PAREN = new Token("("),
            CLOSE_PAREN = new Token(")"),
            SEMICOLON = new Token(";"),
            COLON = new Token(":"),
            COMMA = new Token(","),
            LT = new Token("<"),
            GT = new Token(">");

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
            var nodes = ParserBuilder.CreateParser(rules);
            var parser = new ParserNodeParser(nodes, Start, this.output.WriteLine);

            var listener = new TreeListener();
            parser.Parse(new[] { ID, SEMICOLON, OPEN_BRACKET, ID, OPEN_BRACKET, ID, ID, CLOSE_BRACKET, CLOSE_BRACKET, SEMICOLON }, listener);

            this.output.WriteLine(listener.Root.Flatten().ToString());
            listener.Root.Flatten().ToString()
                .ShouldEqual("Start(List<Stmt>(Stmt(Exp(ID), ;), Stmt(Exp([, List<Exp>(Exp(ID), Exp([, List<Exp>(Exp(ID), Exp(ID)), ])), ]), ;)))");
        }

        // test case from https://stackoverflow.com/questions/8496065/why-is-this-lr1-grammar-not-lalr1
        [Fact]
        public void TestNonLalr1()
        {
            //S->aEa | bEb | aFb | bFa
            //E->e
            //F->e

            var s = new NonTerminal("S");
            var e = new NonTerminal("E");
            var f = new NonTerminal("F");
            var a = new Token("a");
            var b = new Token("b");
            var eToken = new Token("e");

            var rules = new[]
            {
                new Rule(Start, s),
                new Rule(s, a, e, a),
                new Rule(s, b, e, b),
                new Rule(s, a, f, b),
                new Rule(s, b, f, a),
                new Rule(e, eToken),
                new Rule(f, eToken),
            };
            var nodes = ParserBuilder.CreateParser(rules);
            var parser = new ParserNodeParser(nodes, Start, this.output.WriteLine);

            var listener = new TreeListener();
            parser.Parse(new[] { a, eToken, a }, listener);

            this.output.WriteLine(listener.Root.Flatten().ToString());
            listener.Root.Flatten().ToString()
                .ShouldEqual("Start(S(a, E(e), a))");

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

            var nodes = ParserBuilder.CreateParser(rules);
            //var parser = new ParserGenerator(rules).Create();

            var parser1 = new ParserNodeParser(nodes, Start);
            var listener1 = new TreeListener();
            // [];
            parser1.Parse(new[] { OPEN_BRACKET, CLOSE_BRACKET, SEMICOLON }, listener1);
            this.output.WriteLine(listener1.Root.Flatten().ToString());
            listener1.Root.Flatten().ToString().ShouldEqual("Start(Stmt(Exp([, List<Exp>(), ]), ;))");

            this.output.WriteLine(Environment.NewLine + "///////////////// CASE 2 /////////////////" + Environment.NewLine);

            var parser2 = new ParserNodeParser(nodes, Start, this.output.WriteLine);
            var listener2 = new TreeListener();
            // [ [ id; ] [ [] id ] ];
            parser2.Parse(new[] { OPEN_BRACKET, OPEN_BRACKET, ID, SEMICOLON, CLOSE_BRACKET, OPEN_BRACKET, OPEN_BRACKET, CLOSE_BRACKET, ID, CLOSE_BRACKET, CLOSE_BRACKET, SEMICOLON }, listener2);
            this.output.WriteLine(listener2.Root.Flatten().ToString());
            listener2.Root.Flatten().ToString().ShouldEqual("Start(Stmt(Exp([, List<Exp>(Exp([, Stmt(Exp(ID), ;), List<Stmt>(), ]), Exp([, List<Exp>(Exp([, List<Exp>(), ]), Exp(ID)), ])), ]), ;))");
        }

        // tests parsing a non-ambiguous grammar with both generics and comparison
        [Fact]
        public void TestGenericsAmbiguity()
        {
            var name = new NonTerminal("Name");
            var nameListOption = new NonTerminal("Opt<List<Name>>");
            var nameList = new NonTerminal("List<Name>");
            var genericParameters = new NonTerminal("Gen");
            var optionalGenericParameters = new NonTerminal("Opt<Gen>");

            var rules = new[]
            {
                new Rule(Exp, name),
                new Rule(name, ID, optionalGenericParameters),
                new Rule(optionalGenericParameters),
                new Rule(optionalGenericParameters, genericParameters),
                new Rule(genericParameters, LT, nameListOption, GT),
                new Rule(nameListOption),
                new Rule(nameListOption, nameList),
                new Rule(nameList, name),
                new Rule(nameList, name, COMMA, nameList)
            };

            var nodes1 = ParserBuilder.CreateParser(rules);
            var parser1 = new ParserNodeParser(nodes1, Exp, this.output.WriteLine);
            var listener1 = new TreeListener();
            parser1.Parse(new[] { ID, LT, ID, COMMA, ID, GT }, listener1);
            this.output.WriteLine(listener1.Root.Flatten().ToString());
            listener1.Root.Flatten().ToString().ShouldEqual("Exp(Name(ID, Opt<Gen>(Gen(<, Opt<List<Name>>(List<Name>(Name(ID, Opt<Gen>()), ,, Name(ID, Opt<Gen>()))), >))))");

            var cmp = new NonTerminal("Cmp");
            var ambiguousRules = rules.Concat(new[]
                {
                    new Rule(cmp, LT),
                    new Rule(cmp, GT),
                    new Rule(Exp, ID, cmp, Exp),
                })
                .ToArray();
            
            this.output.WriteLine("*********** MORE AMBIGUOUS CASE ***********");
            var nodes2 = ParserBuilder.CreateParser(ambiguousRules);
            var parser2 = new ParserNodeParser(nodes2, Exp, this.output.WriteLine);
            var listener2 = new TreeListener();
            // id < id<id<id>>
            parser2.Parse(new[] { ID, LT, ID, LT, ID, LT, ID, GT, GT }, listener2);
            this.output.WriteLine(listener2.Root.Flatten().ToString());
            listener2.Root.Flatten()
                .ToString()
                .ShouldEqual("Exp(ID, Cmp(<), Exp(Name(ID, Opt<Gen>(Gen(<, Opt<List<Name>>(List<Name>(Name(ID, Opt<Gen>(Gen(<, Opt<List<Name>>(List<Name>(Name(ID, Opt<Gen>()))), >))))), >)))))");
        }

        // C# generics creates an ambiguity like
        // f(g<h, i>(j))
        // in this case, we create a similar but simpler grammar with a similar ambiguity
        // f<g>(h) (could be call(f<g>, h) or compare(f, compare(g, h))
        [Fact]
        public void TestTrueGenericsAmbiguity()
        {
            var name = new NonTerminal("Name");
            var argList = new NonTerminal("List<Exp>");
            var genericParameters = new NonTerminal("Gen");
            var optionalGenericParameters = new NonTerminal("Opt<Gen>");
            var cmp = new NonTerminal("Cmp");

            var rules = new[]
            {
                new Rule(Start, Exp),

                new Rule(Exp, ID),
                new Rule(Exp, OPEN_PAREN, Exp, CLOSE_PAREN),
                new Rule(Exp, ID, cmp, Exp),
                new Rule(Exp, name, OPEN_PAREN, argList, CLOSE_PAREN),

                new Rule(cmp, LT),
                new Rule(cmp, GT),

                new Rule(argList, Exp),
                new Rule(argList, Exp, COMMA, argList),

                new Rule(name, ID, optionalGenericParameters),

                new Rule(optionalGenericParameters),
                new Rule(optionalGenericParameters, genericParameters),

                new Rule(genericParameters, LT, ID, GT)
            };

            // currently this fails due to duplicate prefixes
            var ex = Assert.Throws<NotSupportedException>(() => ParserBuilder.CreateParser(rules));
            this.output.WriteLine(ex.Message);

            var nodes = ParserBuilder.CreateParser(
                rules,
                new Dictionary<IReadOnlyList<Symbol>, Rule>
                {
                    {
                        // id<id>(id could be call(id<id>, id) or compare(id, compare(id, id))
                        new[] { ID, LT, ID, GT, OPEN_PAREN, ID },
                        rules.Single(r => r.Symbols.SequenceEqual(new Symbol[] { name, OPEN_PAREN, argList, CLOSE_PAREN }))
                    },
                    {
                        // id<id>(( could be call(id<id>, (...)) or compare(id, compare(id, (...)))
                        new[] { ID, LT, ID, GT, OPEN_PAREN, OPEN_PAREN },
                        rules.Single(r => r.Symbols.SequenceEqual(new Symbol[] { name, OPEN_PAREN, argList, CLOSE_PAREN }))
                    },
                }
            );
            var parser = new ParserNodeParser(nodes, Start, this.output.WriteLine);
            var listener = new TreeListener();

            // compares: id < id > (id) vs. call: id<id>(id) => our resolution says call
            parser.Parse(new[] { ID, LT, ID, GT, OPEN_PAREN, ID, CLOSE_PAREN }, listener);
            this.output.WriteLine(listener.Root.Flatten().ToString());
            listener.Root.Flatten().ToString().ShouldEqual("Start(Exp(Name(ID, Opt<Gen>(Gen(<, ID, >))), (, List<Exp>(Exp(ID)), )))");
        }

        /// <summary>
        /// Tests parsing an ambiguous grammar with a casting ambiguity similar to what we have in C#/Java
        /// 
        /// E. g. (x)-y could be casting -y to x or could be subtracting y from x.
        /// </summary>
        [Fact]
        public void TestCastAmbiguity()
        {
            var cast = new NonTerminal("Cast");
            var term = new NonTerminal("Term");
            var minus = new Token("-");

            var rules = new[]
            {
                new Rule(Start, Exp),

                new Rule(Exp, term),
                new Rule(Exp, term, minus, Exp),

                new Rule(term, ID),
                new Rule(term, OPEN_PAREN, Exp, CLOSE_PAREN),
                new Rule(term, cast),

                new Rule(cast, OPEN_PAREN, ID, CLOSE_PAREN, Exp),
            };

            var ex = Assert.Throws<NotSupportedException>(() => ParserBuilder.CreateParser(rules));
            this.output.WriteLine(ex.Message);

            var nodes = ParserBuilder.CreateParser(
                rules,
                new Dictionary<IReadOnlyList<Symbol>, Rule>
                {
                    {
                        new Symbol[] { term, minus },
                        rules.Single(r => r.Symbols.SequenceEqual(new Symbol[] { term, minus, Exp }))
                    },
                }
            );
            var parser = new ParserNodeParser(nodes, Start, this.output.WriteLine);
            var listener = new TreeListener();

            // (id)((id) - id)
            parser.Parse(new[] { OPEN_PAREN, ID, CLOSE_PAREN, OPEN_PAREN, OPEN_PAREN, ID, CLOSE_PAREN, minus, ID, CLOSE_PAREN }, listener);
            this.output.WriteLine(listener.Root.Flatten().ToString());
            listener.Root.Flatten().ToString()
                .ShouldEqual("Start(Exp(Term(Cast((, ID, ), Exp(Term((, Exp(Term((, Exp(Term(ID)), )), -, Exp(Term(ID))), )))))))");
        }

        /// <summary>
        /// Tests an ambiguity similar to what C# has with await. For example:
        /// 
        /// await(foo) could be Call("await", "foo") or Await("foo")
        /// </summary>
        [Fact]
        public void TestAwaitParensAmbiguity()
        {
            var call = new NonTerminal("Call");
            var arguments = new NonTerminal("Arguments");
            var argumentsTail = new NonTerminal("ArgumentsTail");
            var identifier = new NonTerminal("Identifier");
            var await = new Token("await");

            var awaitRule = new Rule(Exp, new Symbol[] { await, Exp });
            var rules = new[]
            {
                new Rule(Start, StmtList),
                
                new Rule(StmtList, Stmt, StmtList),
                new Rule(StmtList),

                new Rule(Stmt, Exp, SEMICOLON),

                new Rule(Exp, identifier),
                new Rule(Exp, OPEN_PAREN, Exp, CLOSE_PAREN),
                new Rule(Exp, call),
                awaitRule,

                new Rule(call, identifier, OPEN_PAREN, arguments, CLOSE_PAREN),

                new Rule(arguments),
                new Rule(arguments, Exp, argumentsTail),
                new Rule(argumentsTail),
                new Rule(argumentsTail, COMMA, Exp, argumentsTail),

                new Rule(identifier, ID),
                new Rule(identifier, await),
            };

            var nodes = ParserBuilder.CreateParser(
                rules,
                new Dictionary<IReadOnlyList<Symbol>, Rule>
                {
                    { new Symbol[] { await, OPEN_PAREN, ID, CLOSE_PAREN }, awaitRule },
                    { new Symbol[] { await, OPEN_PAREN, OPEN_PAREN, Exp, CLOSE_PAREN, CLOSE_PAREN }, awaitRule },
                    { new Symbol[] { await, OPEN_PAREN, ID, OPEN_PAREN, arguments, CLOSE_PAREN, CLOSE_PAREN }, awaitRule },
                    { new Symbol[] { await, OPEN_PAREN, await, CLOSE_PAREN }, awaitRule },
                    { new Symbol[] { await, OPEN_PAREN, await, Exp, CLOSE_PAREN }, awaitRule },
                    { new Symbol[] { await, OPEN_PAREN, await, OPEN_PAREN, arguments, CLOSE_PAREN, CLOSE_PAREN }, awaitRule },
                    { new Symbol[] { await, OPEN_PAREN, await, OPEN_PAREN, Exp, CLOSE_PAREN, CLOSE_PAREN }, awaitRule },
                }
            );
            var parser = new ParserNodeParser(nodes, Start, this.output.WriteLine);

            var listener = new TreeListener();
            parser.Parse(new[] { await, OPEN_PAREN, ID, CLOSE_PAREN, SEMICOLON, await, OPEN_PAREN, ID, COMMA, ID, CLOSE_PAREN, SEMICOLON }, listener);
            this.output.WriteLine(listener.Root.Flatten().ToString());
            listener.Root.Flatten().ToString().ShouldEqual("Start(List<Stmt>(Stmt(Exp(await, Exp((, Exp(Identifier(ID)), ))), ;), Stmt(Exp(Call(Identifier(await), (, Arguments(Exp(Identifier(ID)), ArgumentsTail(,, Exp(Identifier(ID)), ArgumentsTail())), ))), ;)))");
        }

        [Fact]
        public void TestDanglingElseAmbiguity()
        {
            var @if = new Token("If");
            var @else = new Token("Else");
            var then = new Token("Then");

            var rules = new[]
            {
                new Rule(Start, Exp),
                new Rule(Exp, ID),
                new Rule(Exp, @if, Exp, then, Exp),
                new Rule(Exp, @if, Exp, then, Exp, @else, Exp),
            };

            Assert.Throws<NotSupportedException>(() => ParserBuilder.CreateParser(rules));

            ParserBuilder.CreateParser(
                rules,
                new Dictionary<IReadOnlyList<Symbol>, Rule>
                {
                    { new Symbol[] { @if, Exp, then, Exp, @else }, rules.Single(r => r.Symbols.Contains(@else)) }
                }
            );
        }

        // based on https://www.gnu.org/software/bison/manual/html_node/Mysterious-Conflicts.html#Mysterious-Conflicts
        [Fact]
        public void TestBisonMysteriousConflict()
        {
            //  def: param_spec return_spec ',';
            //  param_spec:
            //  type
            //| name_list ':' type
            //;
            //  return_spec:
            //  type
            //| name ':' type
            //;
            //  type: "id";

            //  name: "id";
            //  name_list:
            //  name
            //| name ',' name_list
            //;

            NonTerminal def = new NonTerminal("def"),
                paramSpec = new NonTerminal("param_spec"),
                returnSpec = new NonTerminal("return_spec"),
                type = new NonTerminal("type"),
                nameList = new NonTerminal("name_list"),
                name = new NonTerminal("name");

            var rules = new[]
            {
                new Rule(def, paramSpec, returnSpec, COMMA),
                new Rule(paramSpec, type),
                new Rule(paramSpec, nameList, COLON, type),
                new Rule(returnSpec, type),
                new Rule(returnSpec, name, COLON, type),
                new Rule(type, ID),
                new Rule(name, ID),
                new Rule(nameList, name),
                new Rule(nameList, name, COMMA, nameList),
            };

            var nodes = ParserBuilder.CreateParser(rules);
        }
    }
}
