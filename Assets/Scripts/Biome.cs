using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Biome
{
    [Range(0, 1)]
    public float startHeight;
    public Color startColor;
    public Color endColor;
    public int numSteps;
}