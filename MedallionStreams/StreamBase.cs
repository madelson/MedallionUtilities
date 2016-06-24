using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.IO
{
    public abstract class StreamBase : Stream
    {
        private StreamCapabilities capabilities;
        private byte[] cachedSingleByteBuffer;

        protected StreamBase(StreamCapabilities capabilities)
        {
            VerifyCapabilities(this.GetType(), capabilities);
            this.capabilities = capabilities;
        }

        protected StreamBase(StreamCapabilities currentCapabilities, StreamCapabilities typeCapabilities)
        {
            if (currentCapabilities.Except(typeCapabilities) != StreamCapabilities.None)
            {
                throw new ArgumentException(nameof(currentCapabilities) + " may not exceed " + nameof(typeCapabilities));
            }
            VerifyCapabilities(this.GetType(), typeCapabilities);
            this.capabilities = currentCapabilities;
        }

        #region ---- Capabilities ----
        public sealed override bool CanRead => this.capabilities.CanRead;

        public sealed override bool CanSeek => this.capabilities.CanSeek;

        public sealed override bool CanTimeout => this.capabilities.CanTimeout;

        public sealed override bool CanWrite => this.capabilities.CanWrite;
        #endregion

        #region ---- Read Methods ----
        protected virtual int InternalRead(byte[] buffer, int offset, int count)
        {
            if (this.capabilities.CanSyncRead) { throw new InvalidOperationException(nameof(InternalRead) + " must be overriden"); }

            return GetResultAndUnwrapException(this.InternalReadAsync(buffer, offset, count, CancellationToken.None));
        }

        protected virtual Task<int> InternalReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (this.capabilities.CanAsyncRead) { throw new InvalidOperationException(nameof(InternalReadAsync) + " must be overriden"); }

            var taskCompletionSource = new TaskCompletionSource<int>();
            try
            {
                taskCompletionSource.SetResult(this.InternalRead(buffer, offset, count));
            }
            catch (Exception ex)
            {
                taskCompletionSource.SetException(ex);
            }

            return taskCompletionSource.Task;
        }

        public sealed override int Read(byte[] buffer, int offset, int count)
        {
            this.RequireReadable();
            ValidateBuffer(buffer, offset, count);

            if (count == 0) { return 0; }

            return this.InternalRead(buffer, offset, count);
        }

        public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            this.RequireReadable();
            ValidateBuffer(buffer, offset, count);
            
            if (cancellationToken.IsCancellationRequested)
            {
                var canceled = new TaskCompletionSource<int>();
                canceled.SetCanceled();
                return canceled.Task;
            }

            if (count == 0) { return CompletedZeroTask; }

            return this.InternalReadAsync(buffer, offset, count, cancellationToken);
        }

        public sealed override int ReadByte()
        {
            this.RequireReadable();

            var cachedSingleByteBuffer = Interlocked.Exchange(ref this.cachedSingleByteBuffer, null);
            var singleByteBuffer = cachedSingleByteBuffer ?? new byte[1];

            var bytesRead = this.InternalRead(singleByteBuffer, offset: 0, count: 1);
            var result = bytesRead == 0 ? -1 : singleByteBuffer[0];
            
            // volatile to prevent re-ordering with our use of the buffer above
            Volatile.Write(ref this.cachedSingleByteBuffer, singleByteBuffer);

            return result;
        }

        public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            // ReadAsync will handle validation
            var readTask = this.ReadAsync(buffer, offset, count, CancellationToken.None);
            var readResult = new AsyncReadWriteResult<Task<int>>(state, readTask, callback, this);
            if (callback != null)
            {
                readTask.ContinueWith((_, continuationState) => ((AsyncReadWriteResult<Task<int>>)continuationState).InvokeCallback(), state: readResult);
            }
            return readResult;
        }

        public sealed override int EndRead(IAsyncResult asyncResult)
        {
            if (asyncResult == null) { throw new ArgumentNullException(nameof(asyncResult)); }
            var readResult = asyncResult as AsyncReadWriteResult<Task<int>>;
            if (readResult == null || readResult.Stream != this) { throw new ArgumentException("must be created by this stream's BeginRead method", nameof(asyncResult)); }

            return GetResultAndUnwrapException(readResult.Task);
        }
        #endregion

        #region ---- Write Methods ----
        protected virtual void InternalWrite(byte[] buffer, int offset, int count)
        {
            if (this.capabilities.CanSyncWrite) { throw new InvalidOperationException(nameof(InternalWrite) + " must be overriden"); }

            WaitAndUnwrapException(this.InternalWriteAsync(buffer, offset, count, CancellationToken.None));
        }

        protected virtual Task InternalWriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (this.capabilities.CanAsyncWrite) { throw new InvalidOperationException(nameof(InternalWriteAsync) + " must be overriden"); }

            var taskCompletionSource = new TaskCompletionSource<bool>();
            try
            {
                this.InternalWrite(buffer, offset, count);
                taskCompletionSource.SetResult(true);
            }
            catch (Exception ex)
            {
                taskCompletionSource.SetException(ex);
            }

            return taskCompletionSource.Task;
        }

        protected virtual void InternalFlush()
        {
            if (this.capabilities.CanSyncWrite) { throw new InvalidOperationException(nameof(InternalFlush) + " must be overriden"); }

            WaitAndUnwrapException(this.InternalFlushAsync(CancellationToken.None));
        }

        protected virtual Task InternalFlushAsync(CancellationToken cancellationToken)
        {
            if (this.capabilities.CanAsyncWrite) { throw new InvalidOperationException(nameof(InternalFlushAsync) + " must be overriden"); }

            var taskCompletionSource = new TaskCompletionSource<bool>();
            try
            {
                this.InternalFlush();
                taskCompletionSource.SetResult(true);
            }
            catch (Exception ex)
            {
                taskCompletionSource.SetException(ex);
            }

            return taskCompletionSource.Task;
        }
        
        public sealed override void Write(byte[] buffer, int offset, int count)
        {
            this.RequireWritable();
            ValidateBuffer(buffer, offset, count);

            if (count == 0) { return; }

            this.InternalWrite(buffer, offset, count);
        }

        public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            this.RequireWritable();
            ValidateBuffer(buffer, offset, count);

            if (cancellationToken.IsCancellationRequested)
            {
                var canceled = new TaskCompletionSource<int>();
                canceled.SetCanceled();
                return canceled.Task;
            }

            if (count == 0) { return CompletedZeroTask; }

            return this.InternalWriteAsync(buffer, offset, count, cancellationToken);
        }

        public sealed override void WriteByte(byte value)
        {
            this.RequireWritable();

            var cachedSingleByteBuffer = Interlocked.Exchange(ref this.cachedSingleByteBuffer, null);
            var singleByteBuffer = cachedSingleByteBuffer ?? new byte[1];

            singleByteBuffer[0] = value;
            this.InternalWrite(singleByteBuffer, offset: 0, count: 1);
            
            // volatile to prevent re-ordering with our use of the buffer above
            Volatile.Write(ref this.cachedSingleByteBuffer, singleByteBuffer);
        }

        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            // WriteAsync will handle validation
            var writeTask = this.WriteAsync(buffer, offset, count, CancellationToken.None);
            var writeResult = new AsyncReadWriteResult<Task>(state, writeTask, callback, this);
            if (callback != null)
            {
                writeTask.ContinueWith((_, continuationState) => ((AsyncReadWriteResult<Task>)continuationState).InvokeCallback(), state: writeResult);
            }
            return writeResult;
        }

        public sealed override void EndWrite(IAsyncResult asyncResult)
        {
            Throw.IfNull(asyncResult, nameof(asyncResult));
            var writeResult = asyncResult as AsyncReadWriteResult<Task>;
            Throw.If(writeResult == null || writeResult.Stream != this, nameof(asyncResult), "must be created by this stream's BeginWrite method");

            WaitAndUnwrapException(writeResult.Task);
        }

        public sealed override void Flush()
        {
            this.RequireWritable();

            this.InternalFlush();
        }

        public sealed override Task FlushAsync(CancellationToken cancellationToken)
        {
            this.RequireWritable();

            if (cancellationToken.IsCancellationRequested)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                taskCompletionSource.SetCanceled();
                return taskCompletionSource.Task;
            }

            return this.InternalFlushAsync(cancellationToken);
        }
        #endregion

        #region ---- Seek Methods ----
        protected virtual long InternalGetLength() { throw new NotSupportedException(nameof(InternalGetLength)); }
        protected virtual void InternalSetLength(long value) { throw new NotSupportedException(nameof(InternalSetLength)); }

        protected virtual long InternalGetPosition() { throw new NotSupportedException(nameof(InternalGetPosition)); }
        protected virtual void InternalSetPosition(long value) { throw new NotSupportedException(nameof(InternalSetPosition)); }

        public sealed override long Length
        {
            get
            {
                if (!this.capabilities.CanGetLength) { throw new NotSupportedException(nameof(Length)); }
                return this.InternalGetLength();
            }
        }

        public sealed override void SetLength(long value)
        {
            this.RequireWritable();
            this.RequireSeekable();
            if (value < 0) { throw new ArgumentOutOfRangeException(nameof(value), value, "must be non-negative"); }

            this.InternalSetLength(value);
        }

        public sealed override long Position
        {
            get
            {
                if (!this.capabilities.CanGetPosition) { throw new NotSupportedException(nameof(Position)); }
                return this.InternalGetPosition();
            }
            set
            {
                this.RequireSeekable();
                if (value < 0) { throw new ArgumentOutOfRangeException(nameof(value), value, "must be non-negative"); }
                this.InternalSetPosition(value);
            }
        }

        public sealed override long Seek(long offset, SeekOrigin origin)
        {
            this.RequireSeekable();

            long newPosition;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = CheckedAdd(this.InternalGetPosition(), offset);
                    break;
                case SeekOrigin.End:
                    newPosition = CheckedAdd(this.InternalGetPosition(), offset);
                    break;
                default:
                    throw new ArgumentException($"{nameof(origin)}: invalid origin value '{origin}'");
            }

            if (newPosition < 0)
            {
                throw new InvalidOperationException($"Seek of {offset} from {origin} would move the position before the beginning of the stream");
            }

            this.InternalSetPosition(newPosition);
            return newPosition;
        }

        private static long CheckedAdd(long position, long offset)
        {
            try
            {
                return checked(position + offset);
            }
            catch (OverflowException ex)
            {
                throw new ArgumentException($"Adding the offset ({offset}) to the current position ({position}) resulted in overflow", ex);
            }
        }
        #endregion

        #region ---- Timeout Methods ----
        protected virtual TimeSpan InternalReadTimeout
        {
            get { throw new NotSupportedException(nameof(InternalReadTimeout)); }
            set { throw new NotSupportedException(nameof(InternalReadTimeout)); }
        }

        protected virtual TimeSpan InternalWriteTimeout
        {
            get { throw new NotSupportedException(nameof(InternalWriteTimeout)); }
            set { throw new NotSupportedException(nameof(InternalWriteTimeout)); }
        }

        public sealed override int ReadTimeout
        {
            get
            {
                this.RequireReadable();
                this.RequireTimeoutable();

                return (int)this.InternalReadTimeout.TotalMilliseconds;
            }
            set
            {
                this.RequireReadable();
                this.RequireTimeoutable();
                if (value < Timeout.Infinite) { throw new ArgumentOutOfRangeException(nameof(value), value, $"must be infinite ({Timeout.Infinite}) or non-negative"); }

                this.InternalReadTimeout = TimeSpan.FromMilliseconds(value);
            }
        }

        public sealed override int WriteTimeout
        {
            get
            {
                this.RequireWritable();
                this.RequireTimeoutable();

                return (int)this.InternalWriteTimeout.TotalMilliseconds;
            }
            set
            {
                this.RequireWritable();
                this.RequireTimeoutable();
                if (value < Timeout.Infinite) { throw new ArgumentOutOfRangeException(nameof(value), value, $"must be infinite ({Timeout.Infinite}) or non-negative"); }

                this.InternalWriteTimeout = TimeSpan.FromMilliseconds(value);
            }
        }
        #endregion

        #region ---- Other Stream Methods ----
        /// <summary>
        /// Sealed because the preferred extension point for cleanup logic is <see cref="Dispose(bool)"/>
        /// </summary>
        public sealed override void Close()
        {
            base.Close();
        }

        protected override void Dispose(bool disposing)
        {
            // this is necessary because CanRead/Write/Seek should return false for a closed stream
            this.capabilities = new StreamCapabilities { CanTimeout = this.CanTimeout };
            base.Dispose(disposing);
        }

        /// <summary>
        /// Sealed because the base implementation is reasonable, and there is no way to override <see cref="Stream.CopyTo(Stream, int)"/>
        /// </summary>
        public sealed override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return base.CopyToAsync(destination, bufferSize, cancellationToken);
        }
        #endregion

        #region ---- Argument Validation ----
        private static void ValidateBuffer(byte[] buffer, int offset, int count)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), offset, "must be non-negative"); }
            if (count < 0) { throw new ArgumentOutOfRangeException(nameof(count), count, "must be non-negative"); }
            if (offset + count > buffer.Length) { throw new ArgumentOutOfRangeException(nameof(offset) + ", " + nameof(count), new { offset, count }, "must refer to a valid segment of " + nameof(buffer)); }
        }

        private void RequireReadable()
        {
            if (!this.CanRead) { throw new NotSupportedException("The stream does not support reading"); }
        }

        private void RequireWritable()
        {
            if (!this.CanWrite) { throw new NotSupportedException("The stream does not support writing"); }
        }

        private void RequireSeekable()
        {
            if (!this.CanSeek) { throw new NotSupportedException("The stream does not support seeking"); }
        }

        private void RequireTimeoutable()
        {
            if (!this.CanTimeout) { throw new NotSupportedException("The stream does not support timeouts"); }
        }
        #endregion

        #region ---- IAsyncResult Implementation ----
        private sealed class AsyncReadWriteResult<TTask> : IAsyncResult
            where TTask : Task
        {
            private readonly object state;
            private readonly TTask task;
            private readonly AsyncCallback callback;
            private readonly StreamBase stream;

            public AsyncReadWriteResult(object state, TTask task, AsyncCallback callback, StreamBase stream)
            {
                this.state = state;
                this.task = task;
                this.callback = callback;
                this.stream = stream;
            }

            public TTask Task => this.task;

            public Stream Stream => this.stream;

            object IAsyncResult.AsyncState => this.state;

            WaitHandle IAsyncResult.AsyncWaitHandle => ((IAsyncResult)this.task).AsyncWaitHandle;

            bool IAsyncResult.CompletedSynchronously => ((IAsyncResult)this.task).CompletedSynchronously;

            bool IAsyncResult.IsCompleted => this.task.IsCompleted;

            public void InvokeCallback()
            {
                this.callback(this);
            }
        }
        #endregion

        #region ---- Async Helpers ----
        private static Task<int> cachedCompletedZeroTask;

        private static Task<int> CompletedZeroTask => (cachedCompletedZeroTask ?? (cachedCompletedZeroTask = Task.FromResult(0)));

        private static void WaitAndUnwrapException(Task task)
        {
            try
            {
                task.Wait();
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions.Count == 1)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
                throw;
            }
        }

        private static TResult GetResultAndUnwrapException<TResult>(Task<TResult> task)
        {
            try
            {
                return task.Result;
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions.Count == 1)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
                throw;
            }
        }
        #endregion

        #region ---- Override Verification ----
        // todo use strings not expressions?
        private static readonly MethodInfo ReadMethod = Method(s => s.InternalRead(null, 0, 0)),
            ReadAsyncMethod = Method(s => s.InternalReadAsync(null, 0, 0, default(CancellationToken))),
            WriteMethod = Method(s => s.InternalWrite(null, 0, 0)),
            WriteAsyncMethod = Method(s => s.InternalWriteAsync(null, 0, 0, default(CancellationToken))),
            FlushMethod = Method(s => s.InternalFlush()),
            FlushAsyncMethod = Method(s => s.InternalFlushAsync(default(CancellationToken))),
            GetPositionMethod = Method(s => s.InternalGetPosition()),
            SetPositionMethod = Method(s => s.InternalSetPosition(0)),
            GetLengthMethod = Method(s => s.InternalGetLength()),
            SetLengthMethod = Method(s => s.InternalSetLength(0));

        private static readonly PropertyInfo ReadTimeoutProperty = Property(s => s.InternalReadTimeout),
            WriteTimeoutProperty = Property(s => s.InternalWriteTimeout);

        private static readonly HashSet<MethodInfo> OverridableMethods = new HashSet<MethodInfo>
        {
            ReadMethod, ReadAsyncMethod, WriteMethod, WriteAsyncMethod, FlushMethod, FlushAsyncMethod,
            GetPositionMethod, SetPositionMethod, GetLengthMethod, SetLengthMethod,
            ReadTimeoutProperty.GetMethod, ReadTimeoutProperty.SetMethod, WriteTimeoutProperty.GetMethod, WriteTimeoutProperty.SetMethod,
        };

        private static readonly Hashtable VerifiedTypeCache = new Hashtable();

        private static void VerifyCapabilities(Type streamType, StreamCapabilities capabilities)
        {
            // safe because Hashtable is safe for multiple concurrent readers and one writer
            var cachedCapabilitiesObj = VerifiedTypeCache[streamType];
            if (cachedCapabilitiesObj != null)
            {
                var cachedCapabilities = (StreamCapabilities)cachedCapabilitiesObj;
                if (cachedCapabilities != capabilities)
                {
                    throw new ArgumentException($"{streamType} was previously validated for the following capabilities: {capabilities}. It cannot be revalidated for a different set of capabilities ({capabilities})");
                }
                return;
            }

            var requiredMethods = new HashSet<MethodInfo>();
            if (capabilities.CanSyncRead) { requiredMethods.Add(ReadMethod); }
            if (capabilities.CanAsyncRead) { requiredMethods.Add(ReadAsyncMethod); }
            if (capabilities.CanSyncWrite)
            {
                requiredMethods.Add(WriteMethod);
                requiredMethods.Add(FlushMethod);
            }
            if (capabilities.CanAsyncWrite)
            {
                requiredMethods.Add(WriteAsyncMethod);
                requiredMethods.Add(FlushAsyncMethod);
            }
            if (capabilities.CanGetLength)
            {
                requiredMethods.Add(GetLengthMethod);
            }
            if (capabilities.CanGetPosition)
            {
                requiredMethods.Add(GetPositionMethod);
            }
            if (capabilities.CanSeek)
            {
                if (capabilities.CanWrite)
                {
                    requiredMethods.Add(SetLengthMethod);
                }
                requiredMethods.Add(SetPositionMethod);
            }
            if (capabilities.CanTimeout)
            {
                if (capabilities.CanRead)
                {
                    requiredMethods.Add(ReadTimeoutProperty.GetMethod);
                    requiredMethods.Add(ReadTimeoutProperty.SetMethod);
                }
                if (capabilities.CanWrite)
                {
                    requiredMethods.Add(WriteTimeoutProperty.GetMethod);
                    requiredMethods.Add(WriteTimeoutProperty.SetMethod);
                }
            }

            var potentialOverrides = new HashSet<MethodInfo>(
                streamType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => m.DeclaringType != typeof(StreamBase))
            );
            
            var overrides = streamType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.DeclaringType != typeof(StreamBase))
                .Select(s => s.GetBaseDefinition())
                .Where(OverridableMethods.Contains);
            foreach (var method in overrides)
            {
                if (!requiredMethods.Remove(method))
                {
                    throw new ArgumentException($"{streamType} override of method {method} does not match the specified capabilities");
                }
            }

            if (requiredMethods.Count > 0)
            {
                var missingMethods = string.Join(", ", requiredMethods);
                throw new ArgumentException($"Type {streamType} is missing the following overrides that are required to implement the provided capabilities: {missingMethods}");
            }
        
            lock (VerifiedTypeCache.SyncRoot)
            {
                VerifiedTypeCache[streamType] = null;
            }
        }

        private static MethodInfo Method(Expression<Action<StreamBase>> methodCall)
        {
            return ((MethodCallExpression)methodCall.Body).Method;
        }

        private static PropertyInfo Property<TProperty>(Expression<Func<StreamBase, TProperty>> property)
        {
            return (PropertyInfo)((MemberExpression)property.Body).Member;
        }
        #endregion
    }
}
