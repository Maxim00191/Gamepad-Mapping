using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Updater.Install;

internal static class UpdaterInstallLock
{
    public static IDisposable AcquireInstallationMutexOrThrow(string targetDirectory)
    {
        var normalized = System.IO.Path.GetFullPath(targetDirectory).ToLowerInvariant();
        var nameHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        var mutexName = $@"Local\GamepadMapping_UpdateInstall_{nameHash}";

        var mutex = new Mutex(initiallyOwned: false, mutexName);
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(2));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
                throw new UpdaterInstallRunner.UpdateLockConflictException("Another update process is already running for this target directory.");
            return new MutexReleaser(mutex);
        }
        catch
        {
            if (!acquired)
                mutex.Dispose();
            throw;
        }
    }

    private sealed class MutexReleaser : IDisposable
    {
        private readonly Mutex _mutex;
        private bool _disposed;

        public MutexReleaser(Mutex mutex)
        {
            _mutex = mutex;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
            }
            _mutex.Dispose();
        }
    }
}
