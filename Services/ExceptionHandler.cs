using Serilog;

namespace CountingBot.Services
{
    public static class ExceptionHandler
    {

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