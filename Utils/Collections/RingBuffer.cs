using System;
using System.Runtime.CompilerServices;

namespace CodexFramework.Utils.Collections
{
    public class RingBuffer<T>
    {
        private int _start;
        private int _count;
        private readonly T[] _buffer;
        
        public ref T this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _buffer[(_start + idx) % _buffer.Length];
        }

        public T[] Buffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer;
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Length;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count;
        }

        public bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Count == Capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RingBuffer(int capacity) => _buffer = new T[capacity];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RingBuffer(T[] values) => _buffer = values;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Push(T item)
        {
            var idx = -1;
            if (_count < Capacity)
            {
                _buffer[_count] = item;
                idx = _count;
                _count++;
            }
            else
            {
                _buffer[_start] = item;
                idx = _start;
                _start = (_start + 1) % Capacity;
            }

            return idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(bool full = false)
        {
            _start = _count = 0;
            if (full)
            {
                for (int i = 0; i < _buffer.Length; i++)
                    _buffer[i] = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int idx)
        {
            if (idx < 0 || idx >= _count)
                throw new IndexOutOfRangeException();
            
            this[idx] = default;
            if (idx > _count / 2)
            {
                //move backwards
                for (int i = idx; i < _count; i++)
                    this[i] = this[i + 1];
            }
            else
            {
                //move forward
                for (int i = idx; i > 0; i--)
                    this[i] = this[i - 1];
                _start = (_start + 1) % _buffer.Length;
            }
            
            _count--;
        }
    }
}