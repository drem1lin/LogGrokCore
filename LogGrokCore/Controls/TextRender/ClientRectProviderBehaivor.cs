using System.Runtime.CompilerServices;
using System.Windows;

namespace LogGrokCore.Controls.TextRender;

public static class ClippingRectProviderBehavior
{
    private sealed class RectBox
    {
        public Rect Value;
        public bool HasValue;
    }

    // container -> (its child targets -> last reported clipping rect).
    // Both levels are keyed by reference identity (no GetHashCode collisions) and use
    // ConditionalWeakTable so entries are reclaimed automatically when the container or target
    // is garbage-collected — nothing is rooted in this static, so there is no leak.
    private static readonly
        ConditionalWeakTable<FrameworkElement, ConditionalWeakTable<IClippingRectChangesAware, RectBox>>
        Subscriptions = new();

    public static readonly DependencyProperty ClippingRectProviderProperty = DependencyProperty.RegisterAttached(
        "ClippingRectProvider", typeof(FrameworkElement), typeof(ClippingRectProviderBehavior),
        new FrameworkPropertyMetadata(default(FrameworkElement),
            FrameworkPropertyMetadataOptions.Inherits, ClippingRectProviderChanged));

    public static Rect GetClippingRect(FrameworkElement container, FrameworkElement element)
    {
        var elementRect = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
        var containerRectInElementCoordinates = new Rect(
            container.TranslatePoint(new Point(0, 0), element),
            container.TranslatePoint(new Point(container.ActualWidth, container.ActualHeight), element));
        containerRectInElementCoordinates.Intersect(elementRect);
        return containerRectInElementCoordinates;
    }

    public static void SetClippingRectProvider(DependencyObject element, FrameworkElement value)
        => element.SetValue(ClippingRectProviderProperty, value);

    public static FrameworkElement? GetClippingRectProvider(DependencyObject element)
        => (FrameworkElement?)element.GetValue(ClippingRectProviderProperty);

    private static void ClippingRectProviderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not (IClippingRectChangesAware targetObject and FrameworkElement targetElement))
        {
            return;
        }

        // Detach this target from any container it was previously registered with.
        foreach (var (_, targets) in Subscriptions)
            targets.Remove(targetObject);

        if (e.NewValue is not FrameworkElement containerElement)
        {
            return;
        }

        if (!Subscriptions.TryGetValue(containerElement, out var subscriptionList))
        {
            subscriptionList = new ConditionalWeakTable<IClippingRectChangesAware, RectBox>();
            var targets = subscriptionList;
            containerElement.LayoutUpdated += (_, _) => OnContainerLayoutUpdated(containerElement, targets);
            Subscriptions.Add(containerElement, subscriptionList);
        }

        var clippingRect = GetClippingRect(containerElement, targetElement);
        subscriptionList.AddOrUpdate(targetObject, new RectBox { Value = clippingRect, HasValue = true });
        targetObject.OnChildRectChanged(clippingRect);
    }

    private static void OnContainerLayoutUpdated(FrameworkElement container,
        ConditionalWeakTable<IClippingRectChangesAware, RectBox> targets)
    {
        foreach (var (target, box) in targets)
        {
            if (target is not FrameworkElement element)
                continue;

            var clippingRect = GetClippingRect(container, element);
            if (box.HasValue && box.Value == clippingRect)
                continue;

            box.Value = clippingRect;
            box.HasValue = true;
            target.OnChildRectChanged(clippingRect);
        }
    }
}
