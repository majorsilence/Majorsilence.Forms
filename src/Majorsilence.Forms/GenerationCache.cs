using System.Collections.Concurrent;

namespace Majorsilence.Forms
{
    // A bounded, lock-free cache of two generations, rather than one dictionary emptied when full: new entries go into
    // the recent generation, and when that has had GenerationSize entries added it becomes the older one and the
    // previous older generation is dropped. An entry found in the older generation is added back to the recent one. So
    // text in use (a grid's headers, a report's repeated values) survives a stream of strings seen once, which used to
    // empty the whole cache every 2000 new strings and lay the working set out again. TextMeasurer keeps its blocks and
    // sizes in two of these.
    //
    // Lock-free. MeasureText is also called off the UI thread (report engines measure on whatever thread renders the
    // report), and with one lock around two Dictionaries, eight threads measuring at once spent a quarter of their time
    // queueing for it (ConcurrentMeasureBenchmarks). A swap replaces the whole pair at once, so a reader sees either the
    // old pair or the new one. Two threads that miss on the same key both lay it out and the first one stored wins.
    internal sealed class GenerationCache<TKey, TValue> where TKey : notnull
    {
        internal const int DefaultGenerationSize = 1000;   // two generations: at most 2000 entries, the old single limit

        private sealed class Generations
        {
            internal Generations (ConcurrentDictionary<TKey, TValue>? older, int size)
            {
                Older = older;
                // Sized for a whole generation up front: growing takes every one of the dictionary's locks, which
                // threads adding blocks queued on (9 % of eight threads measuring new strings).
                Recent = new (Environment.ProcessorCount, size + size / 4);
            }

            internal readonly ConcurrentDictionary<TKey, TValue> Recent;
            internal readonly ConcurrentDictionary<TKey, TValue>? Older;

            // Entries added to Recent, counted here because Recent.Count would take every one of the dictionary's locks.
            internal int Added;
        }

        private readonly int generationSize;

        // For tests: run by the thread whose add filled a generation, before that thread starts the next one.
        private readonly Action? generationFilled;

        private volatile Generations generations;

        internal GenerationCache () : this (DefaultGenerationSize, null)
        {
        }

        // For tests: a smaller generation, and a hook that can hold up the thread that filled one.
        internal GenerationCache (int generationSize, Action? generationFilled)
        {
            this.generationSize = generationSize;
            this.generationFilled = generationFilled;
            generations = new (null, generationSize);
        }

        internal bool TryGet (TKey key, out TValue value)
        {
            var current = generations;
            if (current.Recent.TryGetValue (key, out value!))
                return true;

            if (current.Older is { } older && older.TryGetValue (key, out value!)) {
                Add (key, value);
                return true;
            }

            return false;
        }

        // The value now cached for key: value, or the one another thread stored first.
        internal TValue Add (TKey key, TValue value)
        {
            var current = generations;
            if (!current.Recent.TryAdd (key, value))
                return current.Recent.TryGetValue (key, out var stored) ? stored : value;

            // Any add at or past the limit starts the next generation, not only the one that reached it: while that thread
            // was held up between its add and the swap (preempted, or its virtual CPU not scheduled), every other thread
            // kept adding to the full generation, which grew for as long as the hold-up lasted. Now each thread adds at
            // most one entry to a generation after it is full, so the cache holds at most 2 * generationSize entries plus
            // one per thread adding at once. Only the first swap of a generation takes: a later one, the held-up thread's
            // included, would drop the generations started since.
            var added = System.Threading.Interlocked.Increment (ref current.Added);
            if (added >= generationSize) {
                if (added == generationSize)
                    generationFilled?.Invoke ();

                if (generations == current)
                    System.Threading.Interlocked.CompareExchange (ref generations, new Generations (current.Recent, generationSize), current);
            }

            return value;
        }

        internal void Clear () => generations = new Generations (null, generationSize);

        // A key in both generations counts twice.
        internal int Count {
            get {
                var current = generations;
                return current.Recent.Count + (current.Older?.Count ?? 0);
            }
        }
    }
}
