using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    [SerializeField]
    private List<TKey> keys = new List<TKey>();
    [SerializeField]
    private List<TValue> values = new List<TValue>();

    // Called before Unity serializes the object
    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        foreach (var kvp in this)
        {
            keys.Add(kvp.Key);
            values.Add(kvp.Value);
        }
    }

    // Called after Unity deserializes the object
    public void OnAfterDeserialize()
    {
        this.Clear();
        int count = Math.Min(keys.Count, values.Count);
        for (int i = 0; i < count; i++)
        {
            this[keys[i]] = values[i];
        }
    }
}
