// Lunarward：Microsoft.Xaml.Behaviors.WinUI.Managed 只有 net8.0-windows，Linux 不可用。
// 这里实现上游用到的 Interaction/EventTriggerBehavior/ControlStoryboardAction 的最小可用版本。
// 注意：事件触发与动画动作暂未真正接线（编译与布局可用），见 docs/linux-port/11-m1-0-api-gaps.md。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System.Collections.Generic;

namespace Microsoft.Xaml.Interactivity;

public partial class Behavior : DependencyObject
{
    public DependencyObject? AssociatedObject { get; internal set; }

    public virtual void Attach(DependencyObject associatedObject) => AssociatedObject = associatedObject;

    public virtual void Detach() => AssociatedObject = null;
}

public class Behavior<T> : Behavior where T : DependencyObject
{
    public new T? AssociatedObject => (T?)base.AssociatedObject;

    protected virtual void OnAttached()
    {
    }

    protected virtual void OnDetaching()
    {
    }

    public override void Attach(DependencyObject associatedObject)
    {
        base.Attach(associatedObject);
        OnAttached();
    }

    public override void Detach()
    {
        OnDetaching();
        base.Detach();
    }
}

public static class Interaction
{
    private static readonly DependencyProperty BehaviorsProperty = DependencyProperty.RegisterAttached(
        "Behaviors", typeof(IList<Behavior>), typeof(Interaction), new PropertyMetadata(null));

    public static IList<Behavior> GetBehaviors(DependencyObject obj)
    {
        if (obj.GetValue(BehaviorsProperty) is not BehaviorCollection list)
        {
            list = new BehaviorCollection(obj);
            obj.SetValue(BehaviorsProperty, list);
        }
        return list;
    }

    public static void SetBehaviors(DependencyObject obj, IList<Behavior> value)
    {
        if (value is BehaviorCollection)
        {
            obj.SetValue(BehaviorsProperty, value);
            return;
        }
        // XAML 集合语法可能先构造普通列表再整体赋值，这里转换并逐项 Attach
        var collection = new BehaviorCollection(obj);
        if (value is not null)
        {
            foreach (Behavior behavior in value)
            {
                collection.Add(behavior);
            }
        }
        obj.SetValue(BehaviorsProperty, collection);
    }
}


/// <summary>
/// 行为集合。XAML 里 `Interaction.Behaviors` 是集合语法，项目被 Add 时必须真正 Attach 到宿主，
/// 否则 <c>Behavior.OnAttached</c> 不会执行（M1 曾因此让 InAppToast 完全不工作）。
/// </summary>
public sealed class BehaviorCollection : IList<Behavior>
{
    private readonly DependencyObject _owner;

    private readonly List<Behavior> _items = new();

    public BehaviorCollection(DependencyObject owner) => _owner = owner;

    public Behavior this[int index]
    {
        get => _items[index];
        set
        {
            _items[index]?.Detach();
            _items[index] = value;
            value?.Attach(_owner);
        }
    }

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    public void Add(Behavior item)
    {
        _items.Add(item);
        item?.Attach(_owner);
    }

    public void Clear()
    {
        foreach (Behavior item in _items)
        {
            item?.Detach();
        }
        _items.Clear();
    }

    public bool Contains(Behavior item) => _items.Contains(item);

    public void CopyTo(Behavior[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public IEnumerator<Behavior> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public int IndexOf(Behavior item) => _items.IndexOf(item);

    public void Insert(int index, Behavior item)
    {
        _items.Insert(index, item);
        item?.Attach(_owner);
    }

    public bool Remove(Behavior item)
    {
        if (_items.Remove(item))
        {
            item?.Detach();
            return true;
        }
        return false;
    }

    public void RemoveAt(int index)
    {
        Behavior item = _items[index];
        _items.RemoveAt(index);
        item?.Detach();
    }
}


public class EventTriggerBehavior : Behavior
{
    public string? EventName { get; set; }

    public IList<ControlStoryboardAction> Actions { get; } = new List<ControlStoryboardAction>();
}

public class ControlStoryboardAction : Behavior
{
    public Storyboard? Storyboard { get; set; }
}
