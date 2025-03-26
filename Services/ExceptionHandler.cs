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
                // Log the exception
                Console.WriteLine($"Error: {ex.Message}\nStack Trace:{ex.StackTrace}");
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
                // Log the exception
                Console.WriteLine($"Error: {ex.Message}\nStack Trace:{ex.StackTrace}");
                return default!;
            }
        }
    }
}
