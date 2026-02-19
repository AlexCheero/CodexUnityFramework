using System;
using System.Threading;
using UnityEngine.Device;

namespace CodexFramework.Threading
{
    public sealed class FastWorkers : IDisposable
    {
        private readonly Thread[] _threads;
        private readonly int _workerCount;

        private readonly AutoResetEvent _wakeEvent = new(false);

        private volatile int _phase;
        private volatile int _completed;
        private volatile bool _running = true;

        private IWork _job;
        private int _count;

        public FastWorkers() : this(Math.Max(1, SystemInfo.processorCount - 1)){}
        public FastWorkers(int workerCount)
        {
            _workerCount = workerCount;
            _threads = new Thread[workerCount];

            for (int i = 0; i < workerCount; i++)
            {
                int index = i;
                _threads[i] = new Thread(() => WorkerLoop(index));
                _threads[i].IsBackground = true;
                _threads[i].Start();
            }
        }

        private void WorkerLoop(int threadIndex)
        {
            int localPhase = 0;

            while (_running)
            {
                SpinWait spin = new SpinWait();

                // Быстрое ожидание
                while (_phase == localPhase && _running)
                {
                    if (spin.Count < 20)
                    {
                        spin.SpinOnce();
                    }
                    else
                    {
                        // Если долго нет работы — реально засыпаем
                        _wakeEvent.WaitOne();
                        break;
                    }
                }

                if (!_running)
                    break;

                if (_phase == localPhase)
                    continue;

                localPhase = _phase;

                int chunkSize = _count / _workerCount;
                int start = threadIndex * chunkSize;
                int end = (threadIndex == _workerCount - 1)
                    ? _count
                    : start + chunkSize;

                _job.Execute(start, end);

                Interlocked.Increment(ref _completed);
            }
        }

        public void Run(int count, IWork job)
        {
            _count = count;
            _job = job;

            Volatile.Write(ref _completed, 0);

            Interlocked.Increment(ref _phase);

            // Будим все потоки
            for (int i = 0; i < _workerCount; i++)
                _wakeEvent.Set();

            SpinWait spin = new SpinWait();
            while (Volatile.Read(ref _completed) < _workerCount)
                spin.SpinOnce();
        }

        public void Dispose()
        {
            _running = false;

            for (int i = 0; i < _workerCount; i++)
                _wakeEvent.Set();

            foreach (var t in _threads)
                t.Join();

            _wakeEvent.Dispose();
        }
    }
}