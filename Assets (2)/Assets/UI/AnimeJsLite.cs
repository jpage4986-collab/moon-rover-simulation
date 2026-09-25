using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anime.js 4.x 核心动画模型的 Unity/C# 轻量翻译版。
///
/// 对应关系：
///   anime({ targets, ... }) -> AnimeJsLite.AnimateFloat/Vector2/Vector3/Color
///   easing                  -> AnimeEase
///   delay / duration        -> 毫秒参数
///   loop / alternate        -> SetLoop()
///   timeline                -> AnimeJsLite.CreateTimeline()
///
/// 这是按 Anime.js 的公开行为重新实现的 Unity 版本，不依赖 npm、浏览器或 DOTween。
/// </summary>
public enum AnimeEase
{
    Linear,
    InQuad,
    OutQuad,
    InOutQuad,
    InCubic,
    OutCubic,
    InOutCubic,
    InQuart,
    OutQuart,
    InOutQuart,
    InQuint,
    OutQuint,
    InOutQuint,
    OutSine,
    InOutSine,
    OutExpo,
    InOutExpo,
    OutBack,
    OutElastic,
    OutBounce
}

public static class AnimeEasing
{
    public static float Evaluate(AnimeEase ease, float t)
    {
        t = Mathf.Clamp01(t);

        switch (ease)
        {
            case AnimeEase.InQuad: return t * t;
            case AnimeEase.OutQuad: return 1f - (1f - t) * (1f - t);
            case AnimeEase.InOutQuad: return InOut(t, x => x * x);
            case AnimeEase.InCubic: return t * t * t;
            case AnimeEase.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
            case AnimeEase.InOutCubic: return InOut(t, x => x * x * x);
            case AnimeEase.InQuart: return t * t * t * t;
            case AnimeEase.OutQuart: return 1f - Mathf.Pow(1f - t, 4f);
            case AnimeEase.InOutQuart: return InOut(t, x => x * x * x * x);
            case AnimeEase.InQuint: return t * t * t * t * t;
            case AnimeEase.OutQuint: return 1f - Mathf.Pow(1f - t, 5f);
            case AnimeEase.InOutQuint: return InOut(t, x => x * x * x * x * x);
            case AnimeEase.OutSine: return Mathf.Sin(t * Mathf.PI * 0.5f);
            case AnimeEase.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
            case AnimeEase.OutExpo: return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
            case AnimeEase.InOutExpo:
                if (t <= 0f || t >= 1f) return t;
                return t < 0.5f
                    ? Mathf.Pow(2f, 20f * t - 10f) * 0.5f
                    : (2f - Mathf.Pow(2f, -20f * t + 10f)) * 0.5f;
            case AnimeEase.OutBack:
            {
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                float x = t - 1f;
                return 1f + c3 * x * x * x + c1 * x * x;
            }
            case AnimeEase.OutElastic:
                return OutElastic(t);
            case AnimeEase.OutBounce:
                return OutBounce(t);
            default: return t;
        }
    }

    private static float InOut(float t, Func<float, float> inEase)
    {
        if (t < 0.5f) return inEase(t * 2f) * 0.5f;
        return 1f - inEase((1f - t) * 2f) * 0.5f;
    }

    private static float OutElastic(float t)
    {
        if (t <= 0f || t >= 1f) return t;
        const float c4 = 2f * Mathf.PI / 3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    private static float OutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return n1 * t * t + 0.75f;
        }
        if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return n1 * t * t + 0.9375f;
        }

        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }
}

internal interface IAnimePlayable
{
    bool IsPlaying { get; }
    bool IsComplete { get; }
    void Tick(float deltaTime);
    void CancelFromRunner();
}

/// <summary>
/// 一个属性动画。T 可以是 float、Vector2、Vector3 或 Color，也可以是自定义 struct，
/// 只要通过 lerp 函数告诉它如何插值。
/// </summary>
public sealed class AnimeTween<T> : IAnimePlayable where T : struct
{
    private readonly Func<T> _getter;
    private readonly Action<T> _setter;
    private readonly Func<T, T, float, T> _lerp;

    private T _from;
    private readonly T _to;
    private T? _fromOverride;
    private float _durationSeconds;
    private float _delaySeconds;
    private AnimeEase _ease;
    private int _loopCount;
    private bool _alternate;
    private float _elapsed;
    private bool _prepared;
    private bool _playing;
    private bool _complete;
    private bool _timelineOwned;

    public Action<AnimeTween<T>> OnUpdate;
    public Action<AnimeTween<T>> OnComplete;

    public bool IsPlaying { get { return _playing; } }
    public bool IsComplete { get { return _complete; } }
    public T CurrentValue { get; private set; }
    public float DurationMs { get { return _durationSeconds * 1000f; } }
    public float DelayMs { get { return _delaySeconds * 1000f; } }

    internal float TotalDurationMs
    {
        get
        {
            if (_loopCount < 0) return float.PositiveInfinity;
            return DelayMs + DurationMs * (_loopCount + 1);
        }
    }

    internal AnimeTween(
        Func<T> getter,
        Action<T> setter,
        T to,
        Func<T, T, float, T> lerp,
        float durationMs,
        float delayMs,
        AnimeEase ease,
        bool autoplay)
    {
        if (getter == null) throw new ArgumentNullException("getter");
        if (setter == null) throw new ArgumentNullException("setter");
        if (lerp == null) throw new ArgumentNullException("lerp");

        _getter = getter;
        _setter = setter;
        _to = to;
        _lerp = lerp;
        _durationSeconds = Mathf.Max(0f, durationMs / 1000f);
        _delaySeconds = Mathf.Max(0f, delayMs / 1000f);
        _ease = ease;
        _loopCount = 0;

        if (autoplay) Play();
    }

    /// <summary>指定起点，等同于 anime.js 的 from 值。</summary>
    public AnimeTween<T> From(T value)
    {
        _fromOverride = value;
        return this;
    }

    public AnimeTween<T> SetEase(AnimeEase ease)
    {
        _ease = ease;
        return this;
    }

    public AnimeTween<T> SetDelay(float delayMs)
    {
        _delaySeconds = Mathf.Max(0f, delayMs / 1000f);
        return this;
    }

    /// <param name="loopCount">重复次数；0 表示只播放一次，-1 表示无限循环。</param>
    /// <param name="alternate">每次重复是否反向播放。</param>
    public AnimeTween<T> SetLoop(int loopCount, bool alternate = false)
    {
        _loopCount = loopCount < 0 ? -1 : loopCount;
        _alternate = alternate;
        return this;
    }

    public AnimeTween<T> Play()
    {
        Prepare(false);
        AnimeJsRunner.Instance.Register(this);
        return this;
    }

    public AnimeTween<T> Pause()
    {
        _playing = false;
        return this;
    }

    public AnimeTween<T> Resume()
    {
        if (!_complete)
        {
            _playing = true;
            AnimeJsRunner.Instance.Register(this);
        }
        return this;
    }

    public AnimeTween<T> Restart()
    {
        Cancel();
        return Play();
    }

    public AnimeTween<T> Cancel()
    {
        _playing = false;
        AnimeJsRunner.Instance.Unregister(this);
        return this;
    }

    internal void PrepareForTimeline()
    {
        _timelineOwned = true;
        Prepare(true);
    }

    internal void TickAt(float localTime)
    {
        if (!_prepared || _complete) return;
        Evaluate(localTime);
    }

    internal void StopFromTimeline()
    {
        _playing = false;
    }

    void IAnimePlayable.Tick(float deltaTime)
    {
        if (!_playing || _complete) return;
        _elapsed += Mathf.Max(0f, deltaTime);
        Evaluate(_elapsed);
    }

    void IAnimePlayable.CancelFromRunner()
    {
        _playing = false;
    }

    private void Prepare(bool timelineOwned)
    {
        _timelineOwned = timelineOwned;
        _from = _fromOverride.HasValue ? _fromOverride.Value : _getter();
        _elapsed = 0f;
        _prepared = true;
        _playing = true;
        _complete = false;
        CurrentValue = _from;
    }

    private void Evaluate(float localTime)
    {
        if (localTime < _delaySeconds) return;

        float activeTime = localTime - _delaySeconds;
        if (_durationSeconds <= Mathf.Epsilon)
        {
            ApplyValue(1f);
            Complete();
            return;
        }

        int cycle = Mathf.FloorToInt(activeTime / _durationSeconds);
        float cycleTime = activeTime - cycle * _durationSeconds;
        float progress = Mathf.Clamp01(cycleTime / _durationSeconds);

        bool atFinalFrame = _loopCount >= 0 && cycle >= _loopCount;
        if (atFinalFrame && activeTime >= _durationSeconds * (_loopCount + 1))
        {
            cycle = _loopCount;
            progress = 1f;
        }

        if (_alternate && (cycle & 1) == 1) progress = 1f - progress;
        ApplyValue(AnimeEasing.Evaluate(_ease, progress));

        if (atFinalFrame && progress >= 1f && !_alternate)
        {
            Complete();
        }
        else if (_loopCount >= 0 && activeTime >= _durationSeconds * (_loopCount + 1))
        {
            Complete();
        }
    }

    private void ApplyValue(float easedProgress)
    {
        CurrentValue = _lerp(_from, _to, easedProgress);
        _setter(CurrentValue);
        if (OnUpdate != null) OnUpdate(this);
    }

    private void Complete()
    {
        if (_complete) return;
        _complete = true;
        _playing = false;
        if (!_timelineOwned) AnimeJsRunner.Instance.Unregister(this);
        if (OnComplete != null) OnComplete(this);
    }
}

public sealed class AnimeTimeline : IAnimePlayable
{
    private sealed class Entry
    {
        public IAnimeTimelineTween Tween;
        public float AtSeconds;
    }

    private readonly List<Entry> _entries = new List<Entry>();
    private float _durationSeconds;
    private float _elapsed;
    private bool _playing;
    private bool _complete;

    public Action<AnimeTimeline> OnUpdate;
    public Action<AnimeTimeline> OnComplete;

    public bool IsPlaying { get { return _playing; } }
    public bool IsComplete { get { return _complete; } }
    public float CurrentTimeMs { get { return _elapsed * 1000f; } }

    internal AnimeTimeline() { }

    public AnimeTimeline Add<T>(AnimeTween<T> tween, float atMs = 0f) where T : struct
    {
        if (tween == null) throw new ArgumentNullException("tween");
        float atSeconds = Mathf.Max(0f, atMs / 1000f);
        tween.PrepareForTimeline();
        _entries.Add(new Entry { Tween = new AnimeTimelineTween<T>(tween), AtSeconds = atSeconds });
        _durationSeconds = Mathf.Max(_durationSeconds, atSeconds + tween.TotalDurationMs / 1000f);
        return this;
    }

    public AnimeTimeline Play()
    {
        _elapsed = 0f;
        _complete = false;
        _playing = true;
        AnimeJsRunner.Instance.Register(this);
        return this;
    }

    public AnimeTimeline Pause()
    {
        _playing = false;
        return this;
    }

    public AnimeTimeline Resume()
    {
        if (!_complete)
        {
            _playing = true;
            AnimeJsRunner.Instance.Register(this);
        }
        return this;
    }

    public AnimeTimeline Restart()
    {
        Cancel();
        foreach (Entry entry in _entries) entry.Tween.Prepare();
        return Play();
    }

    public AnimeTimeline Cancel()
    {
        _playing = false;
        AnimeJsRunner.Instance.Unregister(this);
        foreach (Entry entry in _entries) entry.Tween.Stop();
        return this;
    }

    void IAnimePlayable.Tick(float deltaTime)
    {
        if (!_playing || _complete) return;
        _elapsed += Mathf.Max(0f, deltaTime);

        foreach (Entry entry in _entries)
        {
            if (_elapsed >= entry.AtSeconds)
                entry.Tween.EvaluateAt(_elapsed - entry.AtSeconds);
        }

        if (OnUpdate != null) OnUpdate(this);
        if (_durationSeconds < float.PositiveInfinity && _elapsed >= _durationSeconds)
        {
            _complete = true;
            _playing = false;
            AnimeJsRunner.Instance.Unregister(this);
            if (OnComplete != null) OnComplete(this);
        }
    }

    void IAnimePlayable.CancelFromRunner()
    {
        _playing = false;
    }
}

internal interface IAnimeTimelineTween
{
    void Prepare();
    void EvaluateAt(float localTime);
    void Stop();
}

internal sealed class AnimeTimelineTween<T> : IAnimeTimelineTween where T : struct
{
    private readonly AnimeTween<T> _tween;

    public AnimeTimelineTween(AnimeTween<T> tween)
    {
        _tween = tween;
    }

    public void Prepare()
    {
        _tween.PrepareForTimeline();
    }

    public void EvaluateAt(float localTime)
    {
        _tween.TickAt(localTime);
    }

    public void Stop()
    {
        _tween.StopFromTimeline();
    }
}

/// <summary>
/// Unity 端的简化 anime() 工厂。
/// 所有 duration/delay/时间线位置均使用毫秒，便于从 Anime.js 代码迁移。
/// </summary>
public static class AnimeJsLite
{
    public static AnimeTween<float> AnimateFloat(
        Func<float> getter,
        Action<float> setter,
        float to,
        float durationMs = 500f,
        AnimeEase ease = AnimeEase.OutQuad,
        float delayMs = 0f,
        bool autoplay = true)
    {
        return new AnimeTween<float>(getter, setter, to, Mathf.Lerp, durationMs, delayMs, ease, autoplay);
    }

    public static AnimeTween<Vector2> AnimateVector2(
        Func<Vector2> getter,
        Action<Vector2> setter,
        Vector2 to,
        float durationMs = 500f,
        AnimeEase ease = AnimeEase.OutQuad,
        float delayMs = 0f,
        bool autoplay = true)
    {
        return new AnimeTween<Vector2>(getter, setter, to, Vector2.LerpUnclamped, durationMs, delayMs, ease, autoplay);
    }

    public static AnimeTween<Vector3> AnimateVector3(
        Func<Vector3> getter,
        Action<Vector3> setter,
        Vector3 to,
        float durationMs = 500f,
        AnimeEase ease = AnimeEase.OutQuad,
        float delayMs = 0f,
        bool autoplay = true)
    {
        return new AnimeTween<Vector3>(getter, setter, to, Vector3.LerpUnclamped, durationMs, delayMs, ease, autoplay);
    }

    public static AnimeTween<Color> AnimateColor(
        Func<Color> getter,
        Action<Color> setter,
        Color to,
        float durationMs = 500f,
        AnimeEase ease = AnimeEase.OutQuad,
        float delayMs = 0f,
        bool autoplay = true)
    {
        return new AnimeTween<Color>(getter, setter, to, Color.LerpUnclamped, durationMs, delayMs, ease, autoplay);
    }

    public static AnimeTimeline CreateTimeline()
    {
        return new AnimeTimeline();
    }

    public static AnimeTween<float> Fade(CanvasGroup group, float alpha, float durationMs = 450f, AnimeEase ease = AnimeEase.OutQuad, float delayMs = 0f, bool autoplay = true)
    {
        if (group == null) throw new ArgumentNullException("group");
        return AnimateFloat(() => group.alpha, value => group.alpha = value, alpha, durationMs, ease, delayMs, autoplay);
    }

    public static AnimeTween<Vector2> Move(RectTransform rect, Vector2 anchoredPosition, float durationMs = 500f, AnimeEase ease = AnimeEase.OutQuad, float delayMs = 0f, bool autoplay = true)
    {
        if (rect == null) throw new ArgumentNullException("rect");
        return AnimateVector2(() => rect.anchoredPosition, value => rect.anchoredPosition = value, anchoredPosition, durationMs, ease, delayMs, autoplay);
    }
}

/// <summary>全局逐帧驱动器，等同 Anime.js 的 engine。</summary>
public sealed class AnimeJsRunner : MonoBehaviour
{
    private static AnimeJsRunner _instance;
    private readonly List<IAnimePlayable> _active = new List<IAnimePlayable>();
    private readonly List<IAnimePlayable> _pendingAdd = new List<IAnimePlayable>();
    private readonly List<IAnimePlayable> _pendingRemove = new List<IAnimePlayable>();

    public static AnimeJsRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject host = new GameObject("AnimeJsRunner");
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<AnimeJsRunner>();
            }
            return _instance;
        }
    }

    internal void Register(IAnimePlayable playable)
    {
        if (playable == null) return;
        if (!_active.Contains(playable) && !_pendingAdd.Contains(playable)) _pendingAdd.Add(playable);
    }

    internal void Unregister(IAnimePlayable playable)
    {
        if (playable == null) return;
        if (!_pendingRemove.Contains(playable)) _pendingRemove.Add(playable);
    }

    private void Update()
    {
        FlushQueues();

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            IAnimePlayable playable = _active[i];
            if (!playable.IsPlaying)
            {
                _active.RemoveAt(i);
                continue;
            }

            playable.Tick(Time.unscaledDeltaTime);
        }

        FlushQueues();
    }

    private void FlushQueues()
    {
        for (int i = 0; i < _pendingRemove.Count; i++) _active.Remove(_pendingRemove[i]);
        _pendingRemove.Clear();

        for (int i = 0; i < _pendingAdd.Count; i++)
        {
            if (!_active.Contains(_pendingAdd[i])) _active.Add(_pendingAdd[i]);
        }
        _pendingAdd.Clear();
    }
}
