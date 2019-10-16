namespace PrimeFinderCore
{
    /// <summary>
    /// Represents a strongly typed batch of computation that can be offloaded to a separate compute unit.
    /// </summary>
    /// <typeparam name="TInput">The type of object this batch accepts as an input.</typeparam>
    /// <typeparam name="TOutput">The type of object this batch produces as an output.</typeparam>
    public interface IBatch<out TInput, out TOutput>
    {
        /// <summary>
        /// The input to this batch. Should only be set during the constructor method or similar.
        /// </summary>
        TInput Input { get; }

        /// <summary>
        /// Whether this batch has been completed.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// The output of this batch. Should only be read after <see cref="IsCompleted"/> is set.
        /// </summary>
        TOutput Output { get; }

        /// <summary>
        /// Processes the batch. After this call completes, <see cref="IsCompleted"/> should have a value of <c>true</c>.
        /// </summary>
        void Process();
    }
}