#nullable enable

using System;
using System.Collections.Generic;

namespace GamepadMapperGUI.Utils.Text;

public sealed class AhoCorasickMatcher
{
    private sealed class Node
    {
        public readonly Dictionary<char, int> Next = new();
        public int Fail;
        public readonly List<int> Out = new();
    }

    private readonly List<Node> _nodes = [new Node()];

    public AhoCorasickMatcher(IReadOnlyList<string> normalizedPatterns)
    {
        ArgumentNullException.ThrowIfNull(normalizedPatterns);
        for (var i = 0; i < normalizedPatterns.Count; i++)
        {
            var p = normalizedPatterns[i] ?? string.Empty;
            if (p.Length == 0)
                continue;
            Add(i, p);
        }

        BuildFailureLinks();
    }

    private int NewNode()
    {
        _nodes.Add(new Node());
        return _nodes.Count - 1;
    }

    private void Add(int patternIndex, ReadOnlySpan<char> text)
    {
        var u = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (!_nodes[u].Next.TryGetValue(c, out var v))
            {
                v = NewNode();
                _nodes[u].Next[c] = v;
            }

            u = v;
        }

        _nodes[u].Out.Add(patternIndex);
    }

    private void BuildFailureLinks()
    {
        var q = new Queue<int>();
        _nodes[0].Fail = 0;

        foreach (var kv in _nodes[0].Next)
        {
            var u = kv.Value;
            _nodes[u].Fail = 0;
            q.Enqueue(u);
        }

        while (q.Count > 0)
        {
            var r = q.Dequeue();
            foreach (var (c, u) in _nodes[r].Next)
            {
                q.Enqueue(u);
                var f = _nodes[r].Fail;
                while (f != 0 && !_nodes[f].Next.ContainsKey(c))
                    f = _nodes[f].Fail;

                if (_nodes[f].Next.TryGetValue(c, out var next))
                    _nodes[u].Fail = next;
                else
                    _nodes[u].Fail = 0;
            }
        }
    }

    public void Search(ReadOnlySpan<char> haystack, Action<int, int> onMatch)
    {
        var state = 0;
        for (var i = 0; i < haystack.Length; i++)
        {
            var c = haystack[i];
            while (state != 0 && !_nodes[state].Next.ContainsKey(c))
                state = _nodes[state].Fail;

            if (_nodes[state].Next.TryGetValue(c, out var next))
                state = next;
            else
                state = 0;

            EmitOutputs(state, i + 1, onMatch);
        }
    }

    private void EmitOutputs(int state, int endExclusive, Action<int, int> onMatch)
    {
        var t = state;
        while (true)
        {
            foreach (var pIdx in _nodes[t].Out)
                onMatch(pIdx, endExclusive);

            if (t == 0)
                break;
            t = _nodes[t].Fail;
        }
    }
}
