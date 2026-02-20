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
        private readonly bool _isDummy;

        private IWork _job;
        private int _count;
        private int _minCountPerThread;
        private int _chunkSize;

        private static WorkersManager _singleton;
        public static WorkersManager Singleton
        {
            get
            {
                _singleton ??= new();
                return _singleton;
            }
        }
        
        private WorkersManager() : this(SystemInfo.processorCount / 2){}
        private WorkersManager(int workerCount)
        {
            if (workerCount <= 0)
            {
                _isDummy = true;
                return;
            }
            
            _isDummy = false;
            
            _threads = new Thread[workerCount];
            _barrier = new Barrier(workerCount + 1);

            for (int i = 0; i < workerCount; i++)
            {
                int threadIndex = i + 1;
                _threads[i] = new Thread(() => WorkerLoop(threadIndex));
                _threads[i].IsBackground = true;
                _threads[i].Start();
            }
            
            Application.quitting += Dispose;
        }

        private void WorkerLoop(int threadIndex)
        {
            while (_running)
            {
                _barrier.SignalAndWait(); // ждём старта

                if (!_running)
                    break;

                var start = threadIndex * _chunkSize;
                var end = start + _chunkSize;
                if (end > _count)
                    end = _count;

                if (start < end)
                {
                    try
                    {
                        _job.Execute(start, end);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                }

                _barrier.SignalAndWait();
            }
        }

        public void Run(int count, int minCountPerThread, IWork job)
        {
            if (_isDummy || count <= minCountPerThread)
            {
                job.Execute(0, count);
                return;
            }
            
            _count = count;
            _minCountPerThread = minCountPerThread;
            _chunkSize = Mathf.Max(_minCountPerThread, Mathf.CeilToInt((float)_count / (_threads.Length + 1)));
            _job = job;
            
            _job.Execute(0, Mathf.Min(_count, _chunkSize));

            _barrier.SignalAndWait(); // стартуем всех
            _barrier.SignalAndWait(); // ждём завершения
        }

        public void Dispose()
        {
            Application.quitting -= Dispose;
            _running = false;

            _barrier.SignalAndWait(); // разбудить чтобы выйти

            foreach (var t in _threads)
                t.Join();

            _barrier.Dispose();
        }
    }

}