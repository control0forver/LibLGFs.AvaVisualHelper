using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using System.Diagnostics;

namespace LibLGFs.AvaVisualHelper;

public static class ViewHelper
{
    public static void ListBox_LayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not ListBox listBox)
            throw new ArgumentException(null, nameof(sender));

        if (!listBox.TryAttachSmoothScrolling())
        {
            Debug.WriteLine($"({typeof(ViewHelper).FullName}.{nameof(ListBox_LayoutUpdated)}) Cannot attach smooth scrolling to {listBox.GetType().Name}.");
        }
    }

    // Clear selection when background is clicked
    public static void ListBox_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            var point = e.GetPosition(listBox);
            var hitTestResult = listBox.GetVisualAt(point);

            if (hitTestResult is not null && hitTestResult.Name == "PART_ContentPresenter")
                listBox.UnselectAll();
        }
    }

    public interface INavigationAnimatable
    {
        public bool OnNavigating(object? _ = null);

        public void OnNavigated(object? _ = null);
    }

    public class SlideAnimation
    {
        public enum ActType
        {
            None = 0,
            In, Out
        }

        public static readonly TimeSpan __AniamtionDuration = TimeSpan.FromSeconds(0.850);
        public const double __AniamtionDelayIncreasing = 0.02; // in Seconds
        public const double __AniamtionOpacityFrom = -1.45;
        public const double __AniamtionOpacityTo = 1.0;
        public const double __AniamtionOffsetFrom = 0.13;
        public const double __AniamtionOffsetTo = 0.0;

        private readonly Lock _lock = new();
        private readonly List<Visual> _targets = [];
        private readonly List<(Task, CancellationTokenSource, dynamic)> _playingAnimations = [];

        //public IReadOnlyList<Visual> Loads => _targets;
        //public IReadOnlyList<(Task, CancellationTokenSource, dynamic)> Playing => _playingAnimations;

        public IReadOnlyList<ResultT> ForEachLoads<ResultT>(Func<Visual, ResultT> action)
        {
            lock (_lock) return _targets.Select(action).ToList();
        }
        public IReadOnlyList<ResultT> ForEachPlaying<ResultT>(Func<(Task, CancellationTokenSource, dynamic), ResultT> action)
        {
            lock (_lock) return _playingAnimations.Select(action).ToList();
        }
        public void ForEachLoads(Action<Visual> action)
        {
            lock (_lock) _targets.ForEach(action);
        }
        public void ForEachPlaying(Action<(Task, CancellationTokenSource, dynamic)> action)
        {
            lock (_lock) _playingAnimations.ForEach(action);
        }

        public void Load(bool validation, params IEnumerable<Visual> visuals)
        {
            lock (_lock)
                foreach (var visual in visuals)
                {
                    if (validation)
                    {
                        if (!visual.IsVisible)
                            continue;
                    }

                    _targets.Add(visual);
                }
        }
        public void Load(params IEnumerable<Visual> visuals)
            => Load(validation: true, visuals);

        public void Remvoe(params IEnumerable<Visual> visuals)
        {
            lock (_lock)
                _targets.RemoveAll(visual => visuals.Contains(visual));
        }

        public void Clear()
        {
            lock (_lock)
                _targets.Clear();
        }

        public void Aim(ActType actType)
        {
            lock (_lock)
            {
                switch (actType)
                {
                    default: return;

                    case ActType.In:
                        foreach (var visual in _targets)
                        {
                            visual.Opacity = __AniamtionOpacityFrom; // that's enough
                        }
                        break;
                }
            }
        }

        public IReadOnlyList<(Task, CancellationTokenSource, dynamic)> Fire(ActType act, Visual? validationParent = null, bool reset = false, bool discard = true, bool fireForget = false)
        {
            List<(Task, CancellationTokenSource, dynamic)> tasks = [];
            if (act == ActType.None)
                return tasks;

            lock (_lock)
            {
                int _count = 0;
                foreach (var visual in _targets)
                {
                    bool _legacy_mode = false;

                    if (visual.RenderTransform is not TransformGroup tGroup)
                    {
                        tGroup = new();

                        if (visual.RenderTransform is not null)
                        {
                            if (visual.RenderTransform is Transform _t)
                                tGroup.Children.Add(_t);
                            else _legacy_mode = true; //throw new NotImplementedException(); // slide in aniamtion skipped for the visual.
                        }

                        visual.RenderTransform = tGroup;
                    }

                    if (tGroup.Children.FirstOrDefault(x => x is TranslateTransform) is not TranslateTransform tTranslate)
                    {
                        tTranslate = new();
                        tGroup.Children.Add(tTranslate);
                    }

                    var slideAnimation = new Animation
                    {
                        Delay = TimeSpan.FromSeconds(__AniamtionDelayIncreasing * _count),
                        Duration = __AniamtionDuration,
                        Easing = new ExponentialEaseOut(),
                        FillMode = FillMode.Both
                    };
                    var _anim_start = new KeyFrame
                    {
                        Cue = new Cue(0.0),
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, reset ? (act is ActType.In ? __AniamtionOpacityFrom : __AniamtionOpacityTo) : visual.Opacity)
                        }
                    };
                    var _anim_end = new KeyFrame
                    {
                        Cue = new Cue(1.0),
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty,  act is ActType.In ? __AniamtionOpacityTo : __AniamtionOpacityFrom)
                        }
                    };
                    slideAnimation.Children.Add(_anim_start);
                    slideAnimation.Children.Add(_anim_end);

                    string keys = Visual.OpacityProperty.ToString();

                    if (!_legacy_mode)
                    {
                        // TODO: visual.Bounds may be empty
                        if (validationParent is not null && visual is Control _control)
                            _control.Arrange(validationParent.Bounds);
                        //if (visual is Control _control)
                        //    _control.Arrange(validationParent?.Bounds ?? new Rect(0, 0, 100000, 100000));

                        var a = act is ActType.In ? (visual.Bounds.Width * __AniamtionOffsetFrom) : __AniamtionOffsetTo;
                        var b = act is ActType.In ? __AniamtionOffsetTo : (visual.Bounds.Width * __AniamtionOffsetFrom);
                        if (!reset)
                        {
                            var _a = tTranslate.X;
                            if (_a != b)
                                a = _a;
                        }

                        // We must be careful here, invalid setters will stuck the animation tasks forever.
                        _anim_start.Setters.Add(new Setter(TranslateTransform.XProperty, a));
                        _anim_end.Setters.Add(new Setter(TranslateTransform.XProperty, b));

                        keys = string.Join('|', keys, TranslateTransform.XProperty.ToString());
                    }

                    //var _invalid_timeout = 
                    //    slideAnimation.Delay + slideAnimation.Duration 
                    //    + TimeSpan.FromMilliseconds(100);

                    var cts = new CancellationTokenSource();
                    var job_task = visual.BeginAnimation(slideAnimation, keys); // TODO: Cancellation

                    var value = (job_task, cts, visual);
                    var cleaner_task = Task.Run(async () =>
                    {
                        await job_task;
                        _ = _playingAnimations.Remove(value);
                    });
                    if (!fireForget)
                    {
                        tasks.Add(value);
                        _playingAnimations.Add(value);
                    }

                    _ = cleaner_task;

                    _count++;
                }

                if (discard)
                    _targets.RemoveAll(x => tasks.Select(y => y.Item3).Contains(x));
            }

            return tasks;
        }
    }
}

public static class GentleAnimationExtensions
{
    private const string AnimatorResourceKey = "__GentleAnimator";

    public static Task BeginAnimation(this Visual visual, Animation animation, string? animationKey)
    {
        ArgumentNullException.ThrowIfNull(visual);
        ArgumentNullException.ThrowIfNull(animation);

        // 获取或创建Animator实例
        var animator = GetOrCreateAnimator(visual);

        return animator.BeginAnimation((Control)visual, animation, animationKey);
    }
    public static Task BeginAnimation(this Visual visual, Animation animation, params IEnumerable<AvaloniaProperty> properties)
        => BeginAnimation(visual, animation, string.Join('|', properties.Select(p => p.Name)));

    /// <summary>
    /// 停止指定属性的动画
    /// </summary>
    /// <param name="visual">目标控件</param>
    /// <param name="property">属性</param>
    public static void StopAnimation(this Visual visual, AvaloniaProperty property)
    {
        ArgumentNullException.ThrowIfNull(visual);
        ArgumentNullException.ThrowIfNull(property);

        if (visual.Resources.TryGetResource(AnimatorResourceKey, null, out var resource) &&
            resource is GentleAnimator animator)
        {
            animator.StopAnimation(property.Name);
        }
    }

    /// <summary>
    /// 停止指定键的动画
    /// </summary>
    /// <param name="visual">目标控件</param>
    /// <param name="animationKey">动画键</param>
    public static void StopAnimation(this Visual visual, string animationKey)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (string.IsNullOrEmpty(animationKey)) throw new ArgumentNullException(nameof(animationKey));

        if (visual.Resources.TryGetResource(AnimatorResourceKey, null, out var resource) &&
            resource is GentleAnimator animator)
        {
            animator.StopAnimation(animationKey);
        }
    }

    /// <summary>
    /// 停止所有动画
    /// </summary>
    /// <param name="visual">目标控件</param>
    public static void StopAllAnimations(this Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);

        if (visual.Resources.TryGetResource(AnimatorResourceKey, null, out var resource) &&
            resource is GentleAnimator animator)
        {
            animator.StopAllAnimations();
        }
    }

    /// <summary>
    /// 检查指定属性是否有正在运行的动画
    /// </summary>
    /// <param name="visual">目标控件</param>
    /// <param name="property">属性</param>
    /// <returns>是否有动画正在运行</returns>
    public static bool IsAnimating(this Visual visual, AvaloniaProperty property)
    {
        ArgumentNullException.ThrowIfNull(visual);
        ArgumentNullException.ThrowIfNull(property);

        if (visual.Resources.TryGetResource(AnimatorResourceKey, null, out var resource) &&
            resource is GentleAnimator animator)
        {
            return animator.IsAnimating(property.Name);
        }

        return false;
    }

    /// <summary>
    /// 获取或创建Animator实例
    /// </summary>
    private static GentleAnimator GetOrCreateAnimator(Visual visual)
    {
        if (visual.Resources.TryGetResource(AnimatorResourceKey, null, out var resource) &&
            resource is GentleAnimator existingAnimator)
        {
            return existingAnimator;
        }

        var newAnimator = new GentleAnimator();
        visual.Resources[AnimatorResourceKey] = newAnimator;

        // 当控件被移除时清理资源
        visual.DetachedFromVisualTree += OnDetachedFromVisualTree;

        return newAnimator;
    }

    private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Visual visual)
        {
            visual.DetachedFromVisualTree -= OnDetachedFromVisualTree;

            // 清理动画资源
            if (visual.Resources.TryGetResource(AnimatorResourceKey, null, out var resource) &&
                resource is GentleAnimator animator)
            {
                animator.StopAllAnimations();
                visual.Resources.Remove(AnimatorResourceKey);
            }
        }
    }
}

public class GentleAnimator
{
    private readonly Dictionary<string, CancellationTokenSource> _animationTokens = [];

    public async Task BeginAnimation(Control control, Animation animation, string? animationKeySuffix = null)
    {
        // 使用属性名作为默认key，或者使用自定义key
        var key = animationKeySuffix ?? $"{control.GetHashCode()}_{animationKeySuffix ?? "Unknown"}";

        // 取消前一个同key的动画
        if (_animationTokens.TryGetValue(key, out var existingCts))
        {
            existingCts.Cancel();
            _animationTokens.Remove(key);
        }

        var cts = new CancellationTokenSource();
        _animationTokens[key] = cts;

        try
        {
            await animation.RunAsync(control, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            // 记录其他异常
            System.Diagnostics.Debug.WriteLine($"动画错误: {ex.Message}");
        }
        finally
        {
            _animationTokens.Remove(key);
        }
    }

    public async Task BeginAnimation(Control control, Animation animation, params IEnumerable<AvaloniaProperty> properties)
        => await BeginAnimation(control, animation, string.Join('|', properties.Select(p => p.Name)));

    public void StopAnimation(string animationKey)
    {
        if (_animationTokens.TryGetValue(animationKey, out var cts))
        {
            cts.Cancel();
            _animationTokens.Remove(animationKey);
        }
    }

    public void StopAllAnimations()
    {
        foreach (var cts in _animationTokens.Values)
        {
            cts.Cancel();
        }
        _animationTokens.Clear();
    }

    public bool IsAnimating(string animationKey)
    {
        return _animationTokens.ContainsKey(animationKey);
    }

    public IEnumerable<string> GetRunningAnimations()
    {
        return _animationTokens.Keys;
    }
}

public static class ListBoxHelpers
{
    public static ScrollViewer? TryGetScrollViewer(this ListBox @this)
    {
        return @this.Find<ScrollViewer>("PART_ScrollViewer") ?? @this.FindDescendantOfType<ScrollViewer>();
    }

    public static bool TryAttachSmoothScrolling(this ListBox @this)
    {
        var _view = @this.TryGetScrollViewer();
        if (_view == null)
            return false;

        var b = Interaction.GetBehaviors(_view);
        if (!b.Any(b => b is Xaml.Behaviors.Interactions.Animated.VerticalScrollViewerAnimatedBehavior))
            b.Add(new Xaml.Behaviors.Interactions.Animated.VerticalScrollViewerAnimatedBehavior());
        return true;
    }
}
