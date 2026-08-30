using System;
using System.Runtime.CompilerServices;

namespace CodexFramework.Utils.Collections
{
    public class RingBuffer<T>
    {
        private int _start;
        private int _count;
        private readonly T[] _buffer;
        private readonly T _empty;
        
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
            get => _count == _buffer.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RingBuffer(int capacity) : this(capacity, default) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RingBuffer(int capacity, T empty)
        {
            _empty = empty;
            _buffer = new T[capacity];
            for (int i = 0; i < capacity; i++)
                _buffer[i] = empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RingBuffer(T[] values) => _buffer = values;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Push(T item)
        {
            if (_count < _buffer.Length)
            {
                var idx = (_start + _count) % _buffer.Length;
                _buffer[idx] = item;
                _count++;
                return idx;
            }

            var overwriteIdx = _start;
            _buffer[_start] = item;
            _start = (_start + 1) % _buffer.Length;
            return overwriteIdx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(bool full = false)
        {
            _start = _count = 0;
            if (full)
            {
                for (int i = 0; i < _buffer.Length; i++)
                    _buffer[i] = _empty;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int idx)
        {
            if (idx < 0 || idx >= _count)
                throw new IndexOutOfRangeException();
            
            this[idx] = _empty;
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
            if (_count == 0)
                _start = 0;
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftForward(int count = 1) => _start = (_start + count) % _buffer.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftBackward(int count = 1)
        {
            _start = (_start - count) % _buffer.Length;
            if (_start < 0) _start += _buffer.Length;
        }

        public struct Enumerator
        {
            private readonly RingBuffer<T> _buffer;
            private int _index;
            public Enumerator(RingBuffer<T> list)
            {
                _buffer = list;
                _index = -1;
            }
            public bool MoveNext()
            {
                _index++;
                return _index < _buffer._count;
            }
            public void Reset() => _index = -1;
            public ref T Current => ref _buffer[_index];
        }

        public Enumerator GetEnumerator() => new Enumerator(this);
    }
}