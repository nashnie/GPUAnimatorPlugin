using UnityEngine;
using System.Collections.Generic;
using System;

[System.Serializable]
public class GPUAnimationFrameData
{
    [System.NonSerialized]
    public Vector3[] decompressed = null;

    public void SetVerts(Vector3[] vector3)
    {
        decompressed = vector3;
    }
}

[System.Serializable]
public class Skeleton : ScriptableObject
{
    public Matrix4x4[] jontFrameMatrixs;
}
