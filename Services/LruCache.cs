using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace doc_bursa.Services
{
    /// <summary>
    /// Проста реалізація LRU кешу для зберігання обмеженої кількості прогнозів.
    /// </summary>
    /// <typeparam name="TKey">Тип ключа.</typeparam>
    /// <typeparam name="TValue">Тип значення.</typeparam>
    public class LruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly TimeSpan? _defaultTtl;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _map;
        private readonly LinkedList<CacheItem> _list;
        private readonly object _syncRoot = new();

        private record CacheItem(TKey Key, TValue Value, DateTimeOffset CreatedAt, TimeSpan? Ttl)
        {
            public bool IsExpired => Ttl.HasValue && DateTimeOffset.UtcNow - CreatedAt > Ttl.Value;
        }

        public LruCache(int capacity = 512, TimeSpan? defaultTtl = null)
        {
            _capacity = capacity;
            _defaultTtl = defaultTtl;
            _map = new Dictionary<TKey, LinkedListNode<CacheItem>>();
            _list = new LinkedList<CacheItem>();
        }

        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _map.Count;
                }
            }
        }

        public bool TryGet(TKey key, out TValue value)
        {
            lock (_syncRoot)
            {
                if (_map.TryGetValue(key, out var node))
                {
                    if (node.Value.IsExpired)
                    {
                        RemoveNode(node);
                        value = default!;
                        return false;
                    }

                    _list.Remove(node);
                    _list.AddFirst(node);
                    value = node.Value.Value;
                    return true;
                }
            }

            value = default!;
            return false;
        }

        public void AddOrUpdate(TKey key, TValue value)
        {
            AddOrUpdate(key, value, null);
        }

        public void AddOrUpdate(TKey key, TValue value, TimeSpan? ttl)
        {
            lock (_syncRoot)
            {
                if (_map.TryGetValue(key, out var existing))
                {
                    _list.Remove(existing);
                }
                else if (_map.Count >= _capacity)
                {
                    RemoveLeastRecentlyUsed();
                }

                var node = new LinkedListNode<CacheItem>(new CacheItem(key, value, DateTimeOffset.UtcNow, ttl ?? _defaultTtl));
                _list.AddFirst(node);
                _map[key] = node;
            }
        }

        public async Task<TValue> GetOrAddAsync(TKey key, Func<Task<TValue>> factory, TimeSpan? ttl = null)
        {
            if (TryGet(key, out var cachedValue))
            {
                return cachedValue;
            }

            var created = await factory();
            AddOrUpdate(key, created, ttl);
            return created;
        }

        private void RemoveLeastRecentlyUsed()
        {
            if (_list.Last is { } lru)
            {
                RemoveNode(lru);
            }
        }

        private void RemoveNode(LinkedListNode<CacheItem> node)
        {
            _map.Remove(node.Value.Key);
            _list.Remove(node);
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _map.Clear();
                _list.Clear();
            }
        }
    }
}

