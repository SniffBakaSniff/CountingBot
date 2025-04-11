using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace CountingBot.Services
{
    /// <summary>
    /// Service for monitoring and logging resource usage of the bot.
    /// </summary>
    public sealed class ResourceMonitorService : IDisposable
    {
        private readonly Process _currentProcess;
        private readonly Timer _resourceMonitorTimer;
        private readonly bool _isWindows;
        private readonly int _processorCount;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the ResourceMonitorService.
        /// </summary>
        /// <param name="monitoringIntervalMinutes">Interval in minutes for periodic resource usage logging.</param>
        public ResourceMonitorService(int monitoringIntervalMinutes = 5)
        {
            _currentProcess = Process.GetCurrentProcess();
            var monitoringInterval = TimeSpan.FromMinutes(monitoringIntervalMinutes);
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            _processorCount = Environment.ProcessorCount;

            // Start periodic resource monitoring
            _resourceMonitorTimer = new Timer(
                _ => LogResourceUsage(),
                null,
                TimeSpan.FromMinutes(1), // Initial delay
                monitoringInterval
            ); // Regular interval

            Log.Information(
                "Resource monitoring initialized with {Interval} minute interval",
                monitoringIntervalMinutes
            );
        }

        /// <summary>
        /// Logs the current resource usage of the bot.
        /// </summary>
        private void LogResourceUsage()
        {
            if (_disposed)
                return;

            try
            {
                // Refresh process info to get current values
                _currentProcess.Refresh();

                // Memory usage
                var workingSet = _currentProcess.WorkingSet64 / 1024 / 1024; // Convert to MB
                var privateMemory = _currentProcess.PrivateMemorySize64 / 1024 / 1024; // Convert to MB

                // CPU usage - this is more complex and platform-dependent
                var cpuUsage = CalculateCpuUsage();

                // Thread count
                var threadCount = _currentProcess.Threads.Count;

                // Handle count (Windows only)
                var handleCount = _isWindows ? _currentProcess.HandleCount : 0;

                // Uptime
                var uptime = DateTime.UtcNow - _currentProcess.StartTime.ToUniversalTime();

                Log.Information(
                    "Resource Usage: {WorkingSetMB}MB working set, {PrivateMemoryMB}MB private memory, "
                        + "CPU: {CpuUsage}%, Threads: {ThreadCount}, Handles: {HandleCount}, Uptime: {Uptime:d\\d\\ h\\h\\ m\\m}",
                    workingSet,
                    privateMemory,
                    cpuUsage,
                    threadCount,
                    handleCount,
                    uptime
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error logging resource usage");
            }
        }

        /// <summary>
        /// Calculates the current CPU usage percentage.
        /// </summary>
        /// <returns>CPU usage as a percentage.</returns>
        private double CalculateCpuUsage()
        {
            try
            {
                // This is a simplified approach - for more accurate CPU measurement,
                // you would need to take samples over time
                TimeSpan startCpuUsage = _currentProcess.TotalProcessorTime;
                DateTime startTime = DateTime.UtcNow;

                // Wait a short period to measure CPU usage
                Thread.Sleep(500);

                TimeSpan endCpuUsage = _currentProcess.TotalProcessorTime;
                DateTime endTime = DateTime.UtcNow;

                double cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                double totalMsPassed = (endTime - startTime).TotalMilliseconds;

                // Adjust for number of cores (using the cached processor count)
                double cpuUsageTotal = cpuUsedMs / (totalMsPassed * _processorCount) * 100;

                return Math.Round(cpuUsageTotal, 1);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error calculating CPU usage");
                return -1;
            }
        }

        /// <summary>
        /// Disposes resources used by the resource monitor.
        /// </summary>
        void IDisposable.Dispose()
        {
            if (_disposed)
                return;

            _resourceMonitorTimer?.Dispose();
            _currentProcess?.Dispose();

            _disposed = true;
        }
    }
}
