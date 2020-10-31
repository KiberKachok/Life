using System;
using UnityEngine;

[Serializable]
public class Biome
{
    public Color endColor;
    public int numSteps;
    public Color startColor;

    [Range(0, 1)] public float startHeight;
}