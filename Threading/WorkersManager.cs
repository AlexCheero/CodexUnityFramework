using System;
using System.Threading;
using UnityEngine;
using SystemInfo = UnityEngine.Device.SystemInfo;

namespace CodexFramework.Threading
{
    public interface IWork
    {
        public void Execute(int start, int end);
    }

    public class WorkersManager : IDisposable
    {
        private readonly Thread[] _threads;
        private readonly Barrier _barrier;
        private volatile bool _running = true;

        private IWork _job;
        private int _count;

        public WorkersManager() : this(Math.Max(1, SystemInfo.processorCount - 1)){}
        public WorkersManager(int workerCount)
        {
            _threads = new Thread[workerCount];
            _barrier = new Barrier(workerCount + 1);

            for (int i = 0; i < workerCount; i++)
            {
                int threadIndex = i;
                _threads[i] = new Thread(() => WorkerLoop(threadIndex));
                _threads[i].IsBackground = true;
                _threads[i].Start();
            }
        }

        private void WorkerLoop(int threadIndex)
        {
            while (_running)
            {
                _barrier.SignalAndWait(); // ждём старта

                if (!_running)
                    break;

                int chunkSize = _count / _threads.Length;
                int start = threadIndex * chunkSize;
                int end = (threadIndex == _threads.Length - 1)
                    ? _count
                    : start + chunkSize;

                try
                {
                    _job.Execute(start, end);
                }
                catch (Exception e)
                {
                   Debug.LogError(e);
                }

                _barrier.SignalAndWait();
            }
        }

        public void Run(int count, IWork job)
        {
            _count = count;
            _job = job;

            _barrier.SignalAndWait(); // стартуем всех
            _barrier.SignalAndWait(); // ждём завершения
        }

        public void Dispose()
        {
            _running = false;

            _barrier.SignalAndWait(); // разбудить чтобы выйти

            foreach (var t in _threads)
                t.Join();

            _barrier.Dispose();
        }
    }

}