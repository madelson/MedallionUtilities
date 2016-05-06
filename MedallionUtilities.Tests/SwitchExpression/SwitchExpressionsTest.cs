//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Xunit;
//using static Medallion.SwitchExpressions;

//namespace Medallion.SwitchExpression
//{
//    public class SwitchExpressionsTest
//    {
//        [Fact]
//        public void BasicTest()
//        {
//            int result = Switch(StringSplitOptions.RemoveEmptyEntries)
//                .Case(StringSplitOptions.None, _ => 5)
//                || Default(10);
//            result.ShouldEqual(10);
//        }

//        [Fact]
//        public void TypeCaseTest()
//        {
//            int result = Switch(new HashSet<string> { "1" })
//                .Case((IList<string> list) => list[0].Length)
//                || Case((IDictionary<int, int> dict) => 12)
//                || Case((ISet<string> s) => s.Count)
//                || Default(0);
//            result.ShouldEqual(1);
//        }
//    }
//}
