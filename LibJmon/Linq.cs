namespace LibJmon.Linq;

internal static class SeqExt
{
    public static TCoord? Find<TCoord,TObj>(this IEnumerable<(TCoord coord, TObj val)> seq, Func<TObj, bool> pred)
        where TCoord : struct =>
            seq.Select(t => (coord: (TCoord?)t.coord, t.val)).FirstOrDefault(t => pred(t.val)).coord;
}

/*
 * Adapted from:
 *     MoreLINQ - Extensions to LINQ to Objects
 *     Copyright (c) 2010 Leopold Bushkin. All rights reserved.
 *     
 *     Licensed under the Apache License, Version 2.0 (the "License");
 *     you may not use this file except in compliance with the License.
 *     You may obtain a copy of the License at
 *     
 *         http://www.apache.org/licenses/LICENSE-2.0
 *     
 *     Unless required by applicable law or agreed to in writing, software
 *     distributed under the License is distributed on an "AS IS" BASIS,
 *     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *     See the License for the specific language governing permissions and
 *     limitations under the License.
 * 
 * With the following changes:
 *   - Edited for terseness
 *   - Changed return type to IEnumerable<IReadonlyList<T>>
 *   - Moved to namespace JmonLib.Impl
 */
internal static class MoreLinq
{
    /// <summary>
    /// Divides a sequence into multiple sequences by using a segment detector based on the original sequence
    /// </summary>
    /// <param name="predicate">
    /// A function, which returns <c>true</c> if the given element
    /// begins a new segment, and <c>false</c> otherwise
    /// </param>
    public static IEnumerable<IReadOnlyList<T>> Segment<T>(this IEnumerable<T> source, Func<T, bool> predicate) =>
        Segment(source, (curr, _) => predicate(curr));

    /// <summary>
    /// Divides a sequence into multiple sequences by using a segment detector based on the original sequence
    /// </summary>
    /// <param name="predicate">
    /// A function, which returns <c>true</c> if the given element or
    /// index indicate a new segment, and <c>false</c> otherwise
    /// </param>
    public static IEnumerable<IReadOnlyList<T>> Segment<T>(this IEnumerable<T> source, Func<T, int, bool> predicate) =>
        Segment(source, (curr, _, index) => predicate(curr, index));

    /// <summary>
    /// Divides a sequence into multiple sequences by using a segment detector based on the original sequence
    /// </summary>
    /// <param name="predicate">
    /// A function, which returns <c>true</c> if the given current element,
    /// previous element or index indicate a new segment, and <c>false</c> otherwise
    /// </param>
    public static IEnumerable<IReadOnlyList<T>> Segment<T>(this IEnumerable<T> source, Func<T, T, int, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        return _(); IEnumerable<IReadOnlyList<T>> _()
        {
            using var e = source.GetEnumerator();
            if (!e.MoveNext())
                yield break;
            
            var previous = e.Current;
            var segment = new List<T> { previous };

            for (var index = 1; e.MoveNext(); index++)
            {
                if (predicate(e.Current, previous, index))
                {
                    yield return segment;
                    segment = new List<T> { e.Current };
                }
                else { segment.Add(e.Current); }
                previous = e.Current;
            }
            yield return segment;
        }
    }
}