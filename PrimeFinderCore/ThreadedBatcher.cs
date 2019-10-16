using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace PrimeFinderCore
{
    /// <summary>
    /// Provides methods to split work apart into batches, to be processed in parallel on separate threads.
    /// </summary>
    /// <typeparam name="TInput">The type of object that will be assigned as the input for each batch.</typeparam>
    /// <typeparam name="TOutput">The type of object that will be assigned as the output for each batch.</typeparam>
    public class ThreadedBatcher<TInput, TOutput> : IDisposable
    {
        /// <summary>
        /// Holds batches that are awaiting processing.
        /// </summary>
        private readonly ConcurrentQueue<IBatch<TInput, TOutput>> pendingBatches;

        /// <summary>
        /// Holds batches that are awaiting being merged into the output.
        /// </summary>
        private readonly ConcurrentQueue<IBatch<TInput, TOutput>> processedBatches;

        /// <summary>
        /// Maps a <see cref="Thread"/> instance to a <see cref="bool"/> determining whether or not it has finished.
        /// </summary>
        private readonly ConcurrentDictionary<Thread, bool> sinkThreadCompletionMap;

        /// <summary>
        /// Holds the sink threads that we will use to collate completed batches.
        /// </summary>
        private readonly Thread[] sinkThreads;

        /// <summary>
        /// Maps a <see cref="Thread"/> instance to a <see cref="bool"/> determining whether or not it has finished.
        /// </summary>
        private readonly ConcurrentDictionary<Thread, bool> sourceThreadCompletionMap;

        /// <summary>
        /// Holds the source threads that we will use to dispense batches to the worker threads.
        /// </summary>
        private readonly Thread[] sourceThreads;

        /// <summary>
        /// The stopwatch to use to measure elapsed time.
        /// </summary>
        private readonly Stopwatch stopwatch;

        /// <summary>
        /// The cancellation token used to abort the managed threads, should the <see cref="Cancel"/> method be called.
        /// </summary>
        private readonly CancellationToken threadCancellationToken;

        /// <summary>
        /// Provides cancellation tokens to abort the managed threads, should the <see cref="Cancel"/> method be called.
        /// </summary>
        private readonly CancellationTokenSource threadCancellationTokenSource;

        /// <summary>
        /// Maps a <see cref="Thread"/> instance to a <see cref="bool"/> determining whether or not it has finished.
        /// </summary>
        private readonly ConcurrentDictionary<Thread, bool> workerThreadCompletionMap;

        /// <summary>
        /// Holds the worker threads that we will use for processing batches.
        /// </summary>
        private readonly Thread[] workerThreads;

        /// <summary>
        /// Whether the sink threads should be currently active.
        /// </summary>
        private volatile bool canSinkThreadsRun;

        /// <summary>
        /// Whether the source threads should be currently active.
        /// </summary>
        private volatile bool canSourceThreadsRun;

        /// <summary>
        /// Whether the worker threads should be currently active.
        /// </summary>
        private volatile bool canWorkerThreadsRun;

        /// <summary>
        /// Initialises a new instance of the <see cref="ThreadedBatcher{TInput,TOutput}"/> class.
        /// </summary>
        /// <param name="sourceThreadCount">
        /// The number of 'source' threads that should be used to distribute batches to worker threads.
        /// </param>
        /// <param name="workerThreadCount">
        /// The number of 'worker' threads that should be used to process pending batches.
        /// </param>
        /// <param name="sinkThreadCount">
        /// The number of 'sink' threads that should be used to collate finished batches from worker threads.
        /// </param>
        public ThreadedBatcher(int sourceThreadCount, int workerThreadCount, int sinkThreadCount)
        {
            threadCancellationTokenSource = new CancellationTokenSource();
            threadCancellationToken = threadCancellationTokenSource.Token;

            stopwatch = new Stopwatch();

            sourceThreads = new Thread[sourceThreadCount];
            workerThreads = new Thread[workerThreadCount];
            sinkThreads = new Thread[sinkThreadCount];

            pendingBatches = new ConcurrentQueue<IBatch<TInput, TOutput>>();
            processedBatches = new ConcurrentQueue<IBatch<TInput, TOutput>>();

            sourceThreadCompletionMap = new ConcurrentDictionary<Thread, bool>();
            workerThreadCompletionMap = new ConcurrentDictionary<Thread, bool>();
            sinkThreadCompletionMap = new ConcurrentDictionary<Thread, bool>();

            canSourceThreadsRun = true;
            canWorkerThreadsRun = true;
            canSinkThreadsRun = true;
        }

        /// <summary>
        /// Destroys a <see cref="ThreadedBatcher{TInput,TOutput}"/> instance.
        /// </summary>
        ~ThreadedBatcher()
        {
            Dispose(false);
        }

        /// <summary>
        /// Delegate method that should be executed by each 'worker' thread for each available batch.
        /// If a <see cref="SentinelBatch"/> instance is encountered, this method will NOT be invoked.
        /// </summary>
        /// <typeparam name="TWorkerState">
        /// The type of object that will be passed to each 'worker' thread, functioning as a common state object. Since this
        /// object will be accessed by many threads, it should contain adequate synchronisation.
        /// </typeparam>
        /// <param name="batchToProcess">The new batch which should be processed.</param>
        /// <param name="commonWorkerState">Optional state that should be made common to every 'worker' thread.</param>
        public delegate void ProcessThreadWorkDelegate<in TWorkerState>(IBatch<TInput, TOutput> batchToProcess,
            TWorkerState commonWorkerState);

        /// <summary>
        /// Delegate method that should be executed by each 'sink' thread to handle each incoming processed batch.
        /// If a <see cref="SentinelBatch"/> instance is encountered, this method will NOT be invoked.
        /// </summary>
        /// <typeparam name="TSinkState">
        /// The type of object that will be passed to each 'sink' thread, functioning as a common state object. Since this
        /// object will be accessed by many threads, it should contain adequate synchronisation.
        /// </typeparam>
        /// <param name="completedBatch"></param>
        /// <param name="commonSinkState">Optional state that should be made common to every 'sink' thread.</param>
        public delegate void SinkThreadWorkDelegate<in TSinkState>(IBatch<TInput, TOutput> completedBatch,
            TSinkState commonSinkState);

        /// <summary>
        /// Delegate method that should be executed by each 'source' thread to generate new batches for the 'worker' threads.
        /// </summary>
        /// <typeparam name="TSourceState">
        /// The type of object that will be passed to each 'source' thread, functioning as a common state object. Since this
        /// object will be accessed by many threads, it should contain adequate synchronisation.
        /// </typeparam>
        /// <param name="sourceThreadId">The current 'source' thread id (unique to each thread).</param>
        /// <param name="commonSourceState">Optional state that should be made common to every 'source' thread.</param>
        /// <returns>
        /// The newly generated batch for the 'worker' threads.
        /// Should return a <see cref="SentinelBatch"/> instance when no more batches can be generated.
        /// </returns>
        public delegate IBatch<TInput, TOutput> SourceThreadWorkDelegate<in TSourceState>(int sourceThreadId,
            TSourceState commonSourceState);

        /// <summary>
        /// The work delegate method for each 'worker' thread.
        /// </summary>
        /// <param name="obj">The optional shared state parameter.</param>
        private void ProcessThreadWork<TDelegate, TState>(object? obj) where TDelegate : Delegate
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "Batcher thread work parameter cannot be null.");
            }

            BatcherThreadParam<TDelegate, TState> threadParam = (BatcherThreadParam<TDelegate, TState>)obj;
            TDelegate workDelegate = threadParam.DelegateMethod;
            TState commonState = threadParam.CommonState;

            try
            {
                do
                {
                    if (pendingBatches.TryPeek(out IBatch<TInput, TOutput>? pendingBatch))
                    {
                        if (pendingBatch == null || pendingBatch is SentinelBatch)
                        {
                            workerThreadCompletionMap[Thread.CurrentThread] = true;
                            if (workerThreadCompletionMap.All(pair => pair.Value))
                            {
                                processedBatches.Enqueue(new SentinelBatch());
                                canWorkerThreadsRun = false;
                            }

                            break;
                        }

                        if (pendingBatches.TryDequeue(out pendingBatch))
                        {
                            workDelegate.DynamicInvoke(pendingBatch, commonState);

                            if (pendingBatch.IsCompleted)
                            {
                                processedBatches.Enqueue(pendingBatch);
                            }
                        }
                    }

                    threadCancellationToken.ThrowIfCancellationRequested();
                } while (canWorkerThreadsRun);
            }
            catch (OperationCanceledException)
            {
                /* ignore all further work and allow the thread to join the original process */
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception thrown during batcher worker thread work:\n{ex}");
            }
        }

        /// <summary>
        /// The work delegate method for each 'sink' thread.
        /// </summary>
        /// <param name="obj">The optional shared state parameter.</param>
        private void SinkThreadWork<TDelegate, TState>(object? obj) where TDelegate : Delegate
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "Batcher thread work parameter cannot be null.");
            }

            BatcherThreadParam<TDelegate, TState> threadParam = (BatcherThreadParam<TDelegate, TState>)obj;
            TDelegate workDelegate = threadParam.DelegateMethod;
            TState commonState = threadParam.CommonState;

            try
            {
                do
                {
                    if (processedBatches.TryPeek(out IBatch<TInput, TOutput>? processedBatch))
                    {
                        if (processedBatch == null || processedBatch is SentinelBatch)
                        {
                            sinkThreadCompletionMap[Thread.CurrentThread] = true;
                            if (sinkThreadCompletionMap.All(pair => pair.Value))
                            {
                                canSinkThreadsRun = false;
                            }

                            break;
                        }

                        if (processedBatches.TryDequeue(out processedBatch))
                        {
                            workDelegate.DynamicInvoke(processedBatch, commonState);
                        }
                    }

                    threadCancellationToken.ThrowIfCancellationRequested();
                } while (canSinkThreadsRun);
            }
            catch (OperationCanceledException)
            {
                /* ignore all further work and allow the thread to join the original process */
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception thrown during batcher sink thread work:\n{ex}");
            }
        }

        /// <summary>
        /// The work delegate method for each 'source' thread.
        /// </summary>
        /// <param name="obj">The optional shared state parameter.</param>
        private void SourceThreadWork<TDelegate, TState>(object? obj) where TDelegate : Delegate
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "Batcher thread work parameter cannot be null.");
            }

            BatcherThreadParam<TDelegate, TState> threadParam = (BatcherThreadParam<TDelegate, TState>)obj;
            TDelegate workDelegate = threadParam.DelegateMethod;
            TState commonState = threadParam.CommonState;
            int sourceThreadId = threadParam.SourceThreadId;

            try
            {
                do
                {
                    IBatch<TInput, TOutput>? newBatch =
                        (IBatch<TInput, TOutput>?)workDelegate.DynamicInvoke(sourceThreadId, commonState);

                    if (newBatch == null || newBatch is SentinelBatch)
                    {
                        sourceThreadCompletionMap[Thread.CurrentThread] = true;
                        if (sourceThreadCompletionMap.All(pair => pair.Value))
                        {
                            pendingBatches.Enqueue(new SentinelBatch());
                            canSourceThreadsRun = false;
                        }

                        break;
                    }

                    pendingBatches.Enqueue(newBatch);

                    threadCancellationToken.ThrowIfCancellationRequested();
                } while (canSourceThreadsRun);
            }
            catch (OperationCanceledException)
            {
                /* ignore all further work and allow the thread to join the original process */
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception thrown during batcher source thread work:\n{ex}");
            }
        }

        /// <summary>
        /// Implementation of the <see cref="IDisposable"/> interface, available to subclasses.
        /// </summary>
        /// <param name="disposing">
        /// Whether this method is being called by the <see cref="Dispose()"/> method, before this object is being finalized.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                threadCancellationTokenSource?.Dispose();
            }
        }

        /// <summary>
        /// Stops all of the managed threads. Blocks.
        /// </summary>
        public void Cancel() => UntilFinished(TimeSpan.FromMilliseconds(0));

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Initialises and starts the managed 'source', 'worker', and 'sink' threads.
        /// </summary>
        /// <typeparam name="TSourceState">
        /// The type of object that will be passed to each 'source' thread, functioning as a common state object. Since this
        /// object will be accessed by many threads, it should contain adequate synchronisation.
        /// </typeparam>
        /// <typeparam name="TWorkerState">
        /// The type of object that will be passed to each 'worker' thread, functioning as a common state object. Since this
        /// object will be accessed by many threads, it should contain adequate synchronisation.
        /// </typeparam>
        /// <typeparam name="TSinkSate">
        /// The type of object that will be passed to each 'sink' thread, functioning as a common state object. Since this
        /// object will be accessed by many threads, it should contain adequate synchronisation.
        /// </typeparam>
        /// <param name="sourceWorkDelegate">
        /// The method that should be executed by each 'source' thread to generate new batches for the 'worker' threads.
        /// Takes the current 'source' thread id (unique to each thread), and should return a <see cref="SentinelBatch"/>
        /// instance when no more batches can be generated.
        /// </param>
        /// <param name="initialSourceState">
        /// Optional state that should be made common to every 'source' thread.
        /// </param>
        /// <param name="batchProcessDelegate">
        /// The method that should be executed by each 'worker' thread for each available batch. If a <see cref="SentinelBatch"/>
        /// instance is encountered, this method will NOT be invoked.
        /// </param>
        /// <param name="initialWorkerState">
        /// Optional state that should be made common to every 'worker' thread.
        /// </param>
        /// <param name="sinkWorkDelegate">
        /// The method that should be executed by each 'sink' thread to handle each incoming processed batch.
        /// If a <see cref="SentinelBatch"/> instance is encountered, this method will NOT be invoked.
        /// </param>
        /// <param name="initialSinkSate">
        /// Optional state that should be made common to every 'sink' thread.
        /// </param>
        public void Initialise<TSourceState, TWorkerState, TSinkSate>(
            SourceThreadWorkDelegate<TSourceState> sourceWorkDelegate, TSourceState initialSourceState,
            ProcessThreadWorkDelegate<TWorkerState> batchProcessDelegate, TWorkerState initialWorkerState,
            SinkThreadWorkDelegate<TSinkSate> sinkWorkDelegate, TSinkSate initialSinkSate)
        {
            for (int i = 0; i < sourceThreads.Length; i++)
            {
                sourceThreads[i] = new Thread(SourceThreadWork<SourceThreadWorkDelegate<TSourceState>, TSourceState>)
                {
                    Name = $"Batcher ({GetHashCode()}) Source Thread {i}"
                };

                var sourceThreadStartParam =
                    new BatcherThreadParam<SourceThreadWorkDelegate<TSourceState>, TSourceState>(i, sourceWorkDelegate,
                        initialSourceState);

                sourceThreads[i].Start(sourceThreadStartParam);
            }

            for (int i = 0; i < workerThreads.Length; i++)
            {
                workerThreads[i] = new Thread(ProcessThreadWork<ProcessThreadWorkDelegate<TWorkerState>, TWorkerState>)
                {
                    Name = $"Batcher ({GetHashCode()}) Worker Thread {i}"
                };

                var workerThreadStartParam =
                    new BatcherThreadParam<ProcessThreadWorkDelegate<TWorkerState>, TWorkerState>(-1,
                        batchProcessDelegate, initialWorkerState);

                workerThreads[i].Start(workerThreadStartParam);
            }

            for (int i = 0; i < sinkThreads.Length; i++)
            {
                sinkThreads[i] = new Thread(SinkThreadWork<SinkThreadWorkDelegate<TSinkSate>, TSinkSate>)
                {
                    Name = $"Batcher ({GetHashCode()}) Sink Thread {i}"
                };

                var sinkThreadStartParam =
                    new BatcherThreadParam<SinkThreadWorkDelegate<TSinkSate>, TSinkSate>(-1, sinkWorkDelegate,
                        initialSinkSate);

                sinkThreads[i].Start(sinkThreadStartParam);
            }

            stopwatch.Restart();
        }

        /// <summary>
        /// Waits until all of the 'source', 'worker', and 'sink' threads join back into the main process. At that point,
        /// the custom 'sink' thread state should be in its final state. Returns the number of elapsed milliseconds. Blocks.
        /// </summary>
        /// <param name="timeout">
        /// The timespan after which this method call should timeout. If <c>null</c>, this call will never timeout.
        /// </param>
        /// <returns>The number of milliseconds that elapsed during batch processing.</returns>
        public long UntilFinished(TimeSpan? timeout = null)
        {
            threadCancellationTokenSource.CancelAfter(timeout ?? Timeout.InfiniteTimeSpan);

            for (int i = 0; i < sinkThreads.Length; i++)
            {
                sinkThreads[i].Join();
            }

            for (int i = 0; i < workerThreads.Length; i++)
            {
                workerThreads[i].Join();
            }

            for (int i = 0; i < sourceThreads.Length; i++)
            {
                sourceThreads[i].Join();
            }

            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Holds a delegate method, and an optional state parameter.
        /// </summary>
        private readonly struct BatcherThreadParam<TDelegate, TState>
        {
            /// <summary>
            /// The common state to be shared across a set of threads.
            /// </summary>
            public readonly TState CommonState;

            /// <summary>
            /// The delegate method to be shared across a set of threads.
            /// </summary>
            public readonly TDelegate DelegateMethod;

            /// <summary>
            /// The id of the source thread, if applicable.
            /// </summary>
            public readonly int SourceThreadId;

            /// <summary>
            /// Initialises a new instance of the <see cref="BatcherThreadParam{TDelegate,TState}"/> struct.
            /// </summary>
            public BatcherThreadParam(int sourceThreadId, TDelegate delegateMethod, TState commonState)
            {
                SourceThreadId = sourceThreadId;

                DelegateMethod = delegateMethod;

                CommonState = commonState;
            }
        }

        /// <summary>
        /// Represents a null batch, signifying the end of processing for a thread.
        /// </summary>
        public struct SentinelBatch : IBatch<TInput, TOutput>
        {
            /// <inheritdoc />
            public TInput Input
            {
                get { throw new InvalidOperationException("Cannot access input of a sentinel batch."); }
            }

            /// <inheritdoc />
            public bool IsCompleted
            {
                get { return true; }
            }

            /// <inheritdoc />
            public TOutput Output
            {
                get { throw new InvalidOperationException("Cannot access output of a sentinel batch."); }
            }

            /// <inheritdoc />
            public void Process()
            {
                throw new InvalidOperationException("Cannot process a sentinel batch.");
            }
        }
    }
}