using System;
using UnityEngine;

namespace Maask.Outlines
{
    [Serializable]
    public class OutlineSettings
    {
        [field: SerializeField, ColorUsage(true, true)] public Color Color { get; private set; }
        [field: SerializeField, Range(0.0f, 50.0f)] public float Thickness { get; private set; }
        [field: SerializeField] public RenderingLayerMask LayerMask { get; private set; }
        [field: SerializeField] public float Blur = 2.0f;
    }
}