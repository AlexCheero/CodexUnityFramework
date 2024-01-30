namespace CodexFramework.Utils.Collections
{
    public class RingBuffer<T>
    {
        private int _start;
        private int _count;
        private T[] _buffer;

        public T[] Buffer => _buffer;

        public int Capacity => _buffer.Length;
        public int Count => _count;
        public bool IsFull => Count == Capacity;

        public RingBuffer(int capacity) => _buffer = new T[capacity];

        public RingBuffer(T[] values) => _buffer = values;

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
    }
}