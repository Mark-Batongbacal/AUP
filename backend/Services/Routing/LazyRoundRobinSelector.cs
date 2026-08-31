namespace backend.Services.Routing;

/// <summary>
/// Consumes one item from each bucket per round without materializing the
/// unvisited tail of any bucket. Bucket and item ordering are supplied by the
/// caller and remain authoritative.
/// </summary>
internal static class LazyRoundRobinSelector
{
    internal static List<TResult> Select<TDescriptor, TResult>(
        IReadOnlyList<IReadOnlyList<TDescriptor>> buckets,
        int maximumItems,
        Func<TDescriptor, TResult> materialize,
        Func<TResult, string> identityKey,
        CancellationToken cancellationToken = default,
        Action? duplicateRejected = null)
    {
        if (maximumItems <= 0 || buckets.Count == 0)
            return [];

        var nextIndexes = new int[buckets.Count];
        var selected = new List<TResult>(Math.Min(maximumItems, buckets.Count));
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (selected.Count < maximumItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var addedAny = false;

            for (var bucketIndex = 0;
                 bucketIndex < buckets.Count;
                 bucketIndex++)
            {
                var bucket = buckets[bucketIndex];
                while (nextIndexes[bucketIndex] < bucket.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var descriptor = bucket[nextIndexes[bucketIndex]++];
                    var candidate = materialize(descriptor);
                    if (!seen.Add(identityKey(candidate)))
                    {
                        duplicateRejected?.Invoke();
                        continue;
                    }

                    selected.Add(candidate);
                    addedAny = true;
                    break;
                }

                if (selected.Count >= maximumItems)
                    break;
            }

            if (!addedAny)
                break;
        }

        return selected;
    }
}
