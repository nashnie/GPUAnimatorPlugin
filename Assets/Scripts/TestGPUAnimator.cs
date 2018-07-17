using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestGPUAnimator : MonoBehaviour {

    public GPUAnimator animator;
	// Use this for initialization
	void Start ()
    {
        GPUAnimatorManager.RegisterAnimator(animator);
	}

    private void OnDestroy()
    {
        GPUAnimatorManager.UnregisterAnimator(animator);
    }
}
