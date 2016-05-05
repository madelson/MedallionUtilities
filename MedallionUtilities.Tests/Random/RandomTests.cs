using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion
{
    public class NativeRandomTest : RandomTest
    {
        protected override Random GetRandom() => new Random(1);
    }

    public class CurrentRandomTest : RandomTest
    {
        protected override Random GetRandom() => Rand.Current;
    }

    public class CreatedRandomTest : RandomTest
    {
        protected override Random GetRandom() => Rand.Create();
    }

    public class JavaRandomTest : RandomTest
    {
        protected override Random GetRandom() => Rand.CreateJavaRandom(seed: 1);
    }

    public class UnseededJavaRandomTest : RandomTest
    {
        protected override Random GetRandom() => Rand.CreateJavaRandom();
    }

    public class CryptoRandomTest : RandomTest
    {
        protected override Random GetRandom() => new System.Security.Cryptography.RNGCryptoServiceProvider().AsRandom();
    }
}
