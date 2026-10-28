using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NodeTree<T>
{
    private readonly T _value;
    private readonly Dictionary<string, List<NodeTree<T>>> _children;
    private readonly List<string> _selectors;
    public NodeTree<T> Parent { get; private set; }
    public IReadOnlyDictionary<string, List<NodeTree<T>>> Children => _children;
    public NodeTree<T> this[string i,int j] => _children[i][j];
    public int ChildCount => _children.Count;
    public T Value => _value;

    public NodeTree(T value, params string[] selectors)
    {
        _value = value;
        _children = new Dictionary<string, List<NodeTree<T>>>();
        _selectors = selectors.ToList();
        foreach (var selector in selectors)
            _children.Add(selector, new List<NodeTree<T>>());

    }

    public NodeTree<T> AddChild(string selector, T value)
    {
        var node = new NodeTree<T>(value) { Parent = this };
        return AddChild(selector, node);
    }
    public NodeTree<T> AddChild(string selector,NodeTree<T> node)
    {
        AddSelector(selector);
        if (!_children[selector].Exists(x => x.Value.Equals(node.Value)))
            _children[selector].Add(node);
        return node;
    }
    public NodeTree<T>[] AddChildren(string selector, params T[] values)
    {
        NodeTree<T>[] nodeArray = new NodeTree<T>[values.Length];
        int i = 0;
        foreach (var value in values)
        {
            NodeTree<T> node = AddChild(selector, value);
            nodeArray[i] = node;
            i++;
        }
        return nodeArray;
    }
    public NodeTree<T> InsertChild(string selector, NodeTree<T> parent, T value)
    {
        var node = new NodeTree<T>(value) { Parent = parent };
        return parent.AddChild(selector, node);
    }
    public bool RemoveChild(string selector, NodeTree<T> node)
    {
        if (CheckChildrenSelector(selector))
            return _children[selector].Remove(node);
        return false;
    }
    public bool ExistsSelectorChilds(string selector)
    {
        if (!CheckChildrenSelector(selector, true)) return false;
        return _children[selector].Count > 0;
    }

    public void Traverse(string selector, Action<T> action)
    {
        action(Value);
        if (!CheckChildrenSelector(selector))
            return;
        foreach (var child in _children[selector])
            child.Traverse(selector, action);
    }
    public void TraverseAll(Action<T> action)
    {
        action(Value);
        if (!ExistsSelectorChilds("outport"))
        {
            return;
        }
        foreach (var child in _children["outport"])
            child.TraverseAll(action);
    }

    public IEnumerable<T> Flatten(string selector)
    {
        if (!CheckChildrenSelector(selector, true))
            return null;
        return new[] { Value }.Concat(_children[selector].SelectMany(x => x.Flatten(selector)));
    }
    private void AddSelector(string selector)
    {
        if (CheckChildrenSelector(selector)) return;
        Debug.Log($"Trying add '{selector}' selector...");
        _selectors.Add(selector);
        _children.Add(selector, new List<NodeTree<T>>());
    }
    private bool CheckChildrenSelector(string selector, bool isError = false)
    {
        if (!_selectors.Contains(selector))
        {
            if(isError) Debug.LogError($"{Value.GetType()} node childrens is not contain '{selector}' selector");
            else Debug.LogWarning($"{Value.GetType()} node childrens is not contain '{selector}' selector");
            return false;
        }
        return true;
    }
}
