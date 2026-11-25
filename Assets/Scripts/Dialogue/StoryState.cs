using System.Collections.Generic;

// 同一关卡内，所有可操控角色共享这些选择。
public class StoryState
{
    // 用状态名查找本次剧情最近一次写入的值。
    private readonly Dictionary<string, string> values = new Dictionary<string, string>();

    // key 是状态名，value 是这次选择写入的值。相同的 key 会被新值覆盖。
    public void Set(string key, string value)
    {
        values[key] = value;
    }

    // key 是要查的状态名，expected 是要匹配的值。状态不存在时返回 false。
    public bool Matches(string key, string expected)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        string actual;
        return values.TryGetValue(key, out actual) && actual == expected;
    }

    internal DialogueStateChange[] CaptureValues()
    {
        var snapshot = new DialogueStateChange[values.Count];
        int index = 0;
        foreach (var value in values)
        {
            snapshot[index++] = new DialogueStateChange { key = value.Key, value = value.Value };
        }
        return snapshot;
    }

    internal void RestoreValues(DialogueStateChange[] snapshot)
    {
        values.Clear();
        if (snapshot == null)
        {
            return;
        }

        foreach (DialogueStateChange value in snapshot)
        {
            if (value != null && !string.IsNullOrWhiteSpace(value.key))
            {
                Set(value.key, value.value);
            }
        }
    }
}
