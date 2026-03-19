using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace LibLGFs.AvaVisualHelper;

public static class VisualHelper
{
    public static IEnumerable<T> FindVisualChildren<T>(this Visual parent, int maxDepth = -1, params IEnumerable<Type> noRecursionVisuals) where T : Visual
    {
        var noRecursionSet = new HashSet<Type>(noRecursionVisuals ?? []);
        return parent.FindVisualChildren<T>(maxDepth, noRecursionSet);
    }

    public static IEnumerable<T> FindVisualChildren<T>(this Visual parent, int maxDepth = -1, params HashSet<Type> noRecursionVisuals) where T : Visual
        => parent.FindVisualChildrenInternal<T>(0, maxDepth, noRecursionVisuals);

    private static IEnumerable<T> FindVisualChildrenInternal<T>(this Visual parent, int currentDepth, int maxDepth, HashSet<Type> noRecursionSet) where T : Visual
    {
        if (maxDepth >= 0 && currentDepth > maxDepth)
            yield break;

        var parentType = parent.GetType();
        var children = parent.GetVisualChildren();
        foreach (var child in children)
        {
            if (child != null)
            {
                if (child is T matchedChild)
                {
                    yield return matchedChild;
                }

                if (!noRecursionSet.Contains(parentType) && !noRecursionSet.Any(type => type.IsAssignableFrom(parentType)))
                    foreach (var descendant in child.FindVisualChildrenInternal<T>(currentDepth + 1, maxDepth, noRecursionSet))
                    {
                        yield return descendant;
                    }
            }
        }
    }

    public static IEnumerable<T> FindLogiclChildren<T>(this Control parent, int maxDepth = -1, bool allowVisual = true, params IEnumerable<Type> noRecursionVisuals) where T : Visual
    {
        var noRecursionSet = new HashSet<Type>(noRecursionVisuals ?? []);
        return parent.FindLogicChildrenInternal<T>(0, maxDepth, allowVisual, noRecursionSet);
    }

    private static IEnumerable<T> FindLogicChildrenInternal<T>(this Control parent, int currentDepth, int maxDepth, bool allowVisual, HashSet<Type> noRecursionSet) where T : Visual
    {
        if (maxDepth >= 0 && currentDepth > maxDepth)
            yield break;

        var parentType = parent.GetType();
        var children = parent.GetLogicalChildren();
        foreach (var child in children)
        {
            if (child != null)
            {
                if (child is T matchedChild)
                {
                    yield return matchedChild;
                }

                if (child is Control controlChild &&
                    !noRecursionSet.Contains(parentType) && !noRecursionSet.Any(type => type.IsAssignableFrom(parentType))
                )
                    foreach (var descendant in controlChild.FindLogicChildrenInternal<T>(currentDepth + 1, maxDepth, allowVisual, noRecursionSet))
                    { yield return descendant; }
                else if (allowVisual && child is Visual visualChild)
                    foreach (var descendant in visualChild.FindVisualChildren<T>(maxDepth - currentDepth, noRecursionSet))
                    { yield return descendant; }
            }
        }
    }
}
