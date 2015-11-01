using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.Async
{
    public static class Cancel
    {
        public static CancellationToken After(TimeSpan timeout)
        {
            var source = new CancellationTokenSource();
            source.CancelAfter(timeout);
            return source.Token;
        }
    }
}
