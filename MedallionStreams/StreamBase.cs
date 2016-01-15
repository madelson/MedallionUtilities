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
        [Flags]
        protected enum StreamCapabilities
        {
            None = 0,
            Read = 1 << 0,
            Write = 1 << 1,
            Seek = 1 << 2,
            Timeout = 1 << 3,
            Async = 1 << 4,
            Threadsafe = 1 << 5,
        }

        private bool canRead, canWrite, canSeek, canTimeout, async, threadsafe;
        private byte[] cachedSingleByteBuffer;

        protected StreamBase(StreamCapabilities capabilities)
        {
            this.canRead = (capabilities & StreamCapabilities.Read) != StreamCapabilities.None;
            this.canWrite = (capabilities & StreamCapabilities.Write) != StreamCapabilities.None;
            this.canSeek = (capabilities & StreamCapabilities.Seek) != StreamCapabilities.None;
            this.canTimeout = (capabilities & StreamCapabilities.Timeout) != StreamCapabilities.None;
            this.async = (capabilities & StreamCapabilities.Async) != StreamCapabilities.None;
            this.threadsafe = (capabilities & StreamCapabilities.Threadsafe) != StreamCapabilities.None;
            this.VerifyCapabilities();
        }

        #region ---- Capabilities ----
        public sealed override bool CanRead { get { return this.canRead; } }

        public sealed override bool CanSeek { get { return this.canSeek; } }

        public sealed override bool CanTimeout { get { return this.canTimeout; } }

        public sealed override bool CanWrite { get { return this.canWrite; } }
        #endregion

        #region ---- Read Methods ----
        protected virtual int InternalRead(byte[] buffer, int offset, int count)
        {
            try
            {
                return this.InternalReadAsync(buffer, offset, count, CancellationToken.None).Result;
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

        protected virtual Task<int> InternalReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
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
            ValidateBuffer(buffer, offset, count);
            if (!this.canRead)
            {
                throw Throw.NotSupported(CannotRead);
            }

            return this.InternalRead(buffer, offset, count);
        }

        public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBuffer(buffer, offset, count);
            if (!this.canRead)
            {
                throw Throw.NotSupported(CannotRead);
            }
            
            if (cancellationToken.IsCancellationRequested)
            {
                var canceled = new TaskCompletionSource<int>();
                canceled.SetCanceled();
                return canceled.Task;
            }

            return this.InternalReadAsync(buffer, offset, count, cancellationToken);
        }

        public sealed override int ReadByte()
        {
            var cachedSingleByteBuffer = this.threadsafe
                ? Interlocked.Exchange(ref this.cachedSingleByteBuffer, null)
                : this.cachedSingleByteBuffer;
            var singleByteBuffer = cachedSingleByteBuffer ?? new byte[1];

            var bytesRead = this.InternalRead(singleByteBuffer, offset: 0, count: 1);
            var result = bytesRead == 0 ? -1 : singleByteBuffer[0];
            
            if (this.threadsafe)
            {
                // volatile to prevent re-ordering with our use of the buffer above
                Volatile.Write(ref this.cachedSingleByteBuffer, singleByteBuffer);
            }
            else
            {
                this.cachedSingleByteBuffer = singleByteBuffer;
            }

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
            Throw.IfNull(asyncResult, nameof(asyncResult));
            var readResult = asyncResult as AsyncReadWriteResult<Task<int>>;
            Throw.If(readResult == null || readResult.Stream != this, nameof(asyncResult), "must be created by this stream's BeginRead method");

            return readResult.Task.Result;
        }
        #endregion

        #region ---- Write Methods ----
        protected virtual void InternalWrite(byte[] buffer, int offset, int count)
        {
            try
            {
                this.InternalWriteAsync(buffer, offset, count, CancellationToken.None).Wait();
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

        protected virtual Task InternalWriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
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
            try
            {
                this.InternalFlushAsync(CancellationToken.None).Wait();
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

        protected virtual Task InternalFlushAsync(CancellationToken cancellationToken)
        {
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
            ValidateBuffer(buffer, offset, count);
            if (!this.canWrite)
            {
                throw Throw.NotSupported(CannotWrite);
            }

            this.InternalWrite(buffer, offset, count);
        }

        public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBuffer(buffer, offset, count);
            if (!this.canWrite)
            {
                throw Throw.NotSupported(CannotWrite);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                var canceled = new TaskCompletionSource<int>();
                canceled.SetCanceled();
                return canceled.Task;
            }

            return this.InternalWriteAsync(buffer, offset, count, cancellationToken);
        }

        public sealed override void WriteByte(byte value)
        {
            var cachedSingleByteBuffer = this.threadsafe
                ? Interlocked.Exchange(ref this.cachedSingleByteBuffer, null)
                : this.cachedSingleByteBuffer;
            var singleByteBuffer = cachedSingleByteBuffer ?? new byte[1];

            singleByteBuffer[0] = value;
            this.InternalWrite(singleByteBuffer, offset: 0, count: 1);
            
            if (this.threadsafe)
            {
                // volatile to prevent re-ordering with our use of the buffer above
                Volatile.Write(ref this.cachedSingleByteBuffer, singleByteBuffer);
            }
            else
            {
                this.cachedSingleByteBuffer = singleByteBuffer;
            }
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

            writeResult.Task.Wait();
        }

        public sealed override void Flush()
        {
            if (!this.canWrite)
            {
                throw Throw.NotSupported(CannotWrite);
            }

            this.InternalFlush();
        }

        public sealed override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (!this.canWrite)
            {
                throw Throw.NotSupported(CannotWrite);
            }

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
        protected virtual long InternalLength
        {
            get { throw Throw.NotSupported(); }
            set { throw Throw.NotSupported(); }
        }

        protected virtual long InternalPosition
        {
            get { throw Throw.NotSupported(); }
            set { throw Throw.NotSupported(); }
        }

        public sealed override long Length
        {
            get
            {
                if (!this.canSeek)
                {
                    throw Throw.NotSupported(CannotSeek);
                }
                return this.InternalLength;
            }
        }

        public sealed override void SetLength(long value)
        {
            Throw.IfOutOfRange(value, nameof(value), min: 0);
            if (!this.canSeek)
            {
                throw Throw.NotSupported(CannotSeek);
            }
            this.InternalLength = value;
        }

        public sealed override long Position
        {
            get
            {
                if (!this.canSeek)
                {
                    throw Throw.NotSupported(CannotSeek);
                }
                return this.InternalPosition;
            }
            set
            {
                Throw.IfOutOfRange(value, nameof(Position), min: 0);
                if (!this.canSeek)
                {
                    throw Throw.NotSupported(CannotSeek);
                }
                this.InternalPosition = value;
            }
        }

        public sealed override long Seek(long offset, SeekOrigin origin)
        {
            if (!this.canSeek)
            {
                throw Throw.NotSupported(CannotSeek);
            }

            long newPosition;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = CheckedAdd(this.InternalPosition, offset);
                    break;
                case SeekOrigin.End:
                    newPosition = CheckedAdd(this.InternalLength, offset);
                    break;
                default:
                    throw new ArgumentException($"{nameof(origin)}: invalid origin value '{origin}'");
            }

            if (newPosition < 0)
            {
                throw new InvalidOperationException($"Seek of {offset} from {origin} would move the position before the beginning of the stream");
            }

            return this.InternalPosition = newPosition;
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
            get { throw Throw.NotSupported(); }
            set { throw Throw.NotSupported(); }
        }

        protected virtual TimeSpan InternalWriteTimeout
        {
            get { throw Throw.NotSupported(); }
            set { throw Throw.NotSupported(); }
        }

        public sealed override int ReadTimeout
        {
            get
            {
                if (!this.canRead)
                {
                    throw Throw.NotSupported(CannotRead);
                }
                if (!this.canTimeout)
                {
                    throw Throw.NotSupported(CannotTimeout);
                }

                return (int)this.InternalReadTimeout.TotalMilliseconds;
            }
            set
            {
                Throw.IfOutOfRange(value, nameof(ReadTimeout), min: Timeout.Infinite);
                if (!this.canRead)
                {
                    throw Throw.NotSupported(CannotRead);
                }
                if (!this.canTimeout)
                {
                    throw Throw.NotSupported(CannotTimeout);
                }

                this.InternalReadTimeout = TimeSpan.FromMilliseconds(value);
            }
        }

        public sealed override int WriteTimeout
        {
            get
            {
                if (!this.canWrite)
                {
                    throw Throw.NotSupported(CannotWrite);
                }
                if (!this.canTimeout)
                {
                    throw Throw.NotSupported(CannotTimeout);
                }

                return (int)this.InternalWriteTimeout.TotalMilliseconds;
            }
            set
            {
                Throw.IfOutOfRange(value, nameof(WriteTimeout), min: Timeout.Infinite);
                if (!this.canWrite)
                {
                    throw Throw.NotSupported(CannotWrite);
                }
                if (!this.canTimeout)
                {
                    throw Throw.NotSupported(CannotTimeout);
                }

                this.InternalWriteTimeout = TimeSpan.FromMilliseconds(value);
            }
        }
        #endregion

        #region ---- Sealed Call Base Methods ----
        /// <summary>
        /// Sealed because the preferred extension point for cleanup logic is <see cref="Dispose(bool)"/>
        /// </summary>
        public sealed override void Close()
        {
            base.Close();
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
            Throw.IfNull(buffer, nameof(buffer));
            Throw.IfOutOfRange(offset, nameof(offset), min: 0);
            Throw.IfOutOfRange(count, nameof(count), min: 0);
            Throw.IfOutOfRange(offset + count, nameof(offset) + ", " + nameof(count), max: buffer.Length);
        }

        private const string CannotRead = "The stream does not support reading",
            CannotWrite = "The stream does not support writing",
            CannotSeek = "The stream does not support seeking",
            CannotTimeout = "The stream does not support timeouts";
        #endregion

        #region ---- IAsyncResult implementation ----
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

            public TTask Task { get { return this.task; } }

            public Stream Stream { get { return this.stream; } }

            object IAsyncResult.AsyncState { get { return this.state; } }

            WaitHandle IAsyncResult.AsyncWaitHandle { get { return ((IAsyncResult)this.task).AsyncWaitHandle; } }

            bool IAsyncResult.CompletedSynchronously { get { return ((IAsyncResult)this.task).CompletedSynchronously; } }

            bool IAsyncResult.IsCompleted { get { return this.task.IsCompleted; } }

            public void InvokeCallback()
            {
                this.callback(this);
            }
        }
        #endregion

        #region ---- Override Verification ----
        private static readonly MethodInfo ReadMethod = Method(s => s.InternalRead(null, 0, 0)),
            ReadAsyncMethod = Method(s => s.InternalReadAsync(null, 0, 0, default(CancellationToken))),
            WriteMethod = Method(s => s.InternalWrite(null, 0, 0)),
            WriteAsyncMethod = Method(s => s.InternalWriteAsync(null, 0, 0, default(CancellationToken))),
            FlushMethod = Method(s => s.InternalFlush()),
            FlushAsyncMethod = Method(s => s.InternalFlushAsync(default(CancellationToken)));

        private static readonly PropertyInfo PositionProperty = Property(s => s.InternalPosition),
            LengthProperty = Property(s => s.InternalLength),
            ReadTimeoutProperty = Property(s => s.InternalReadTimeout),
            WriteTimeoutProperty = Property(s => s.InternalWriteTimeout);

        private static readonly HashSet<MethodInfo> OverridableMethods = new HashSet<MethodInfo>
        {
            ReadMethod, ReadAsyncMethod, WriteMethod, WriteAsyncMethod, FlushMethod, FlushAsyncMethod,
            PositionProperty.GetMethod, PositionProperty.SetMethod, LengthProperty.GetMethod, LengthProperty.SetMethod,
            ReadTimeoutProperty.GetMethod, ReadTimeoutProperty.SetMethod, WriteTimeoutProperty.GetMethod, WriteTimeoutProperty.SetMethod,
        };

        private static readonly Hashtable VerifiedTypeCache = new Hashtable();

        private void VerifyCapabilities()
        {
            var streamType = this.GetType();
            // safe because Hashtable is safe for multiple concurrent readers and one writer
            if (VerifiedTypeCache.ContainsKey(streamType))
            {
                return;
            }

            var requiredMethods = new HashSet<MethodInfo>();
            var optionalMethods = new HashSet<MethodInfo>();
            if (this.canRead)
            {
                if (this.async)
                {
                    requiredMethods.Add(ReadAsyncMethod);
                    optionalMethods.Add(ReadMethod);
                }
                else
                {
                    requiredMethods.Add(ReadMethod);
                }

                if (this.canTimeout)
                {
                    requiredMethods.Add(ReadTimeoutProperty.GetMethod);
                    requiredMethods.Add(ReadTimeoutProperty.SetMethod);
                }
            }
            if (this.canWrite)
            {
                if (this.async)
                {
                    requiredMethods.Add(WriteAsyncMethod);
                    optionalMethods.Add(WriteMethod);
                    requiredMethods.Add(FlushAsyncMethod);
                    optionalMethods.Add(FlushMethod);
                }
                else
                {
                    requiredMethods.Add(WriteMethod);
                    requiredMethods.Add(FlushMethod);
                }

                if (this.canTimeout)
                {
                    requiredMethods.Add(WriteTimeoutProperty.GetMethod);
                    requiredMethods.Add(WriteTimeoutProperty.SetMethod);
                }
            }
            if (this.canSeek)
            {
                requiredMethods.Add(PositionProperty.GetMethod);
                requiredMethods.Add(PositionProperty.SetMethod);
                requiredMethods.Add(LengthProperty.GetMethod);
                requiredMethods.Add(LengthProperty.SetMethod);
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
                if (!requiredMethods.Remove(method) && !optionalMethods.Remove(method))
                {
                    throw new ArgumentException($"{streamType} override of method {method} does not match the provided capabilities");
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
