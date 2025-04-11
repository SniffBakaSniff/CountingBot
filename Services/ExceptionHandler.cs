using Serilog;

namespace CountingBot.Services
{
    /// <summary>
    /// Static utility class providing centralized exception handling functionality.
    /// Ensures consistent error logging and handling across the application.
    /// </summary>
    public static class ExceptionHandler
    {
        /// <summary>
        /// Executes an asynchronous action and handles any exceptions that occur.
        /// Logs errors using Serilog but allows the application to continue running.
        /// </summary>
        /// <param name="action">The asynchronous action to execute</param>
        public static async Task HandleAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while executing the action.");
            }
        }

        /// <summary>
        /// Executes an asynchronous function that returns a value and handles any exceptions that occur.
        /// Logs errors using Serilog and returns the type's default value if an exception occurs.
        /// </summary>
        /// <typeparam name="T">The return type of the function</typeparam>
        /// <param name="action">The asynchronous function to execute</param>
        /// <returns>The result of the function or default(T) if an exception occurs</returns>
        public static async Task<T> HandleAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while executing the action.");
                return default!;
            }
        }
    }
}
