using System.Collections.Generic;

namespace FinDesk.Services
{
    /// <summary>
    /// Проста реалізація LRU кешу для зберігання обмеженої кількості прогнозів.
    /// </summary>
    /// <typeparam name="TKey">Тип ключа.</typeparam>
    /// <typeparam name="TValue">Тип значення.</typeparam>
    public class LruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<(TKey key, TValue value)>> _map;
        private readonly LinkedList<(TKey key, TValue value)> _list;

        public LruCache(int capacity = 512)
        {
            _capacity = capacity;
            _map = new Dictionary<TKey, LinkedListNode<(TKey key, TValue value)>>();
            _list = new LinkedList<(TKey key, TValue value)>();
        }

        public int Count => _map.Count;

        public bool TryGet(TKey key, out TValue value)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);
                value = node.Value.value;
                return true;
            }

            value = default!;
            return false;
        }

        public void AddOrUpdate(TKey key, TValue value)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _list.Remove(existing);
            }
            else if (_map.Count >= _capacity)
            {
                RemoveLeastRecentlyUsed();
            }

            var node = new LinkedListNode<(TKey key, TValue value)>((key, value));
            _list.AddFirst(node);
            _map[key] = node;
        }

        private void RemoveLeastRecentlyUsed()
        {
            var lru = _list.Last;
            if (lru != null)
            {
                _map.Remove(lru.Value.key);
                _list.RemoveLast();
            }
        }
    }
}


