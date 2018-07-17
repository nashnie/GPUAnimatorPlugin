using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;

/// <summary>
/// Nash
/// </summary>
public class GPUAnimationCreator : EditorWindow
{
	[MenuItem("Tools/Create GPU Animator")]
	static void MakeWindow()
	{
		window = GetWindow(typeof(GPUAnimationCreator)) as GPUAnimationCreator;
		window.oColor = GUI.contentColor;
	}

	private static GPUAnimationCreator window;
	private Color oColor;
	private Vector2 scrollpos;
	private Dictionary<string, int> frameSkips = new Dictionary<string, int>();
	private Dictionary<string, bool> bakeAnims = new Dictionary<string, bool>();

    HashSet<string> positionPathHash = new HashSet<string>();
    HashSet<string> rotationPathHash = new HashSet<string>();
    Dictionary<string, int> bonesMap = new Dictionary<string, int>();
    Transform[] joints = null;
    Matrix4x4[] skeletonPoses = null;
    Matrix4x4[] bindSkeletonPoses = null;
    const string positionPath = "m_LocalPosition";
    const string rotationPath = "m_LocalRotation";

    [SerializeField]
	private Vector2 scroll;
	[SerializeField]
	private int fps = 30;
	[SerializeField]
	private int previousGlobalBake = 1;
	[SerializeField]
	private int globalBake = 1;
	[SerializeField]
	private int smoothMeshAngle = -1;
	[SerializeField]
	private GameObject prefab;
	[SerializeField]
	private List<AnimationClip> customClips = new List<AnimationClip>();
	[SerializeField]
	private List<MeshFilter> meshFilters = new List<MeshFilter>();
	[SerializeField]
	private List<SkinnedMeshRenderer> skinnedRenderers = new List<SkinnedMeshRenderer>();
	[SerializeField]
	private GameObject previousPrefab;
	[SerializeField]
	private bool customCompression = false;
	[SerializeField]
	private GameObject spawnedAsset;
	[SerializeField]
	private Animator animator;
	[SerializeField]
	private GPUAnimation.RootMotionMode rootMotionMode = GPUAnimation.RootMotionMode.None;
	[SerializeField]
	private RuntimeAnimatorController animController;
	[SerializeField]
	private Avatar animAvatar;
	[SerializeField]
	private bool requiresAnimator;

    private void OnEnable()
	{
		if (prefab == null && Selection.activeGameObject)
		{
			prefab = Selection.activeGameObject;
			OnPrefabChanged();
		}
	}

	private void OnDisable()
	{
		if (spawnedAsset)
		{
			DestroyImmediate(spawnedAsset.gameObject);
		}
	}

	private string GetAssetPath(string s)
	{
		string path = s;
		string[] split = path.Split('\\');
		path = string.Empty;
		int startIndex = 0;
		for (int i = 0; i < split.Length; i++)
		{
			if (split[i] == "Assets")
				break;
			startIndex++;
		}
		for (int i = startIndex; i < split.Length; i++)
			path += split[i] + "\\";
		path = path.TrimEnd("\\".ToCharArray());
		path = path.Replace("\\", "/");
		return path;
	}

	private void OnGUI()
	{
		if (GUILayout.Button("Batch Bake Selected Objects"))
		{
			previousPrefab = null;
			foreach (var obj in Selection.gameObjects)
			{
				try
				{
					prefab = obj;
					OnPrefabChanged();
					var toBakeClips = GetClips();
					foreach (var clip in toBakeClips)
					{
						frameSkips[clip.name] = 1;
					}
					CreateSnapshots();
				}
				catch (System.Exception e)
				{
					Debug.LogException(e);
				}
			}
		}
		GUI.skin.label.richText = true;
		scroll = GUILayout.BeginScrollView(scroll);
		{
			EditorGUI.BeginChangeCheck();
			prefab = EditorGUILayout.ObjectField("Asset to Bake", prefab, typeof(GameObject), true) as GameObject;
			if (prefab)
			{
				if (string.IsNullOrEmpty(GetPrefabPath()))
				{
					DrawText("Cannot find asset path, are you sure this object is a prefab?", Color.red + Color.white * 0.5f);
					return;
				}
				if (previousPrefab != prefab)
                {
                    OnPrefabChanged();
                }
				if (spawnedAsset == null)
				{
					OnPrefabChanged();
				}
				animController = EditorGUILayout.ObjectField("Animation Controller", animController, typeof(RuntimeAnimatorController), true) as RuntimeAnimatorController;
				if (animController == null)
				{
					GUI.skin.label.richText = true;
					GUILayout.Label("<b>Specify a Animation Controller to auto-populate animation clips</b>");
				}
				if (requiresAnimator)
				{
					if (animAvatar == null)
						GetAvatar();
					animAvatar = EditorGUILayout.ObjectField("Avatar", animAvatar, typeof(Avatar), true) as Avatar;
					if (animAvatar == null)
					{
						GUI.color = Color.red;
						GUI.skin.label.richText = true;
						GUILayout.Label("<color=red>For humanoid and optimized rigs, you must specify an Avatar</color>");
						GUI.color = Color.white;
					}
					rootMotionMode = (GPUAnimation.RootMotionMode)EditorGUILayout.EnumPopup("Root Motion Mode", rootMotionMode);
					switch (rootMotionMode)
					{
						case GPUAnimation.RootMotionMode.Baked:
							{
								GUILayout.Label("Root Motion will be baked into vertices.");
								break;
							}
						case GPUAnimation.RootMotionMode.AppliedToTransform:
							{
								GUILayout.Label("Root Motion will move the MeshAnimator at runtime.");
								break;
							}
					}
				}

                fps = EditorGUILayout.IntSlider("Bake FPS", fps, 1, 500);

				globalBake = EditorGUILayout.IntSlider("Global Frame Skip", globalBake, 1, fps);
				bool bChange = globalBake != previousGlobalBake;
				previousGlobalBake = globalBake;

				EditorGUILayout.LabelField("Custom Clips");
				for (int i = 0; i < customClips.Count; i++)
				{
					GUILayout.BeginHorizontal();
					{
						customClips[i] = (AnimationClip)EditorGUILayout.ObjectField(customClips[i], typeof(AnimationClip), false);
						if (GUILayout.Button("X", GUILayout.Width(32)))
						{
							customClips.RemoveAt(i);
							GUILayout.EndHorizontal();
							break;
						}
					}
					GUILayout.EndHorizontal();
				}

				if (GUILayout.Button("Add Custom Animation Clip"))
                {
                    customClips.Add(null);
                }
				if (GUILayout.Button("Add Selected Animation Clips"))
				{
					foreach (var o in Selection.objects)
					{
						string p = AssetDatabase.GetAssetPath(o);
						if (string.IsNullOrEmpty(p) == false)
						{
							AnimationClip[] clipsToAdd = AssetDatabase.LoadAllAssetRepresentationsAtPath(p).Where(q => q is AnimationClip).Cast<AnimationClip>().ToArray();
							customClips.AddRange(clipsToAdd);
						}
					}
				}
				var clips = GetClips();
				string[] clipNames = bakeAnims.Keys.ToArray();

				bool modified = false;
				scrollpos = GUILayout.BeginScrollView(scrollpos, GUILayout.MinHeight(100), GUILayout.MaxHeight(1000));
				try
				{
					EditorGUI.indentLevel++;
					GUILayout.BeginHorizontal();
					{
						if (GUILayout.Button("Select All", GUILayout.Width(100)))
						{
							foreach (var clipName in clipNames)
								bakeAnims[clipName] = true;
						}
						if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
						{
							foreach (var clipName in clipNames)
								bakeAnims[clipName] = false;
						}
					}
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
					{
						GUILayout.Label("Bake Animation");
						GUILayout.Label("Frame Skip");
					}
					GUILayout.EndHorizontal();
					foreach (var clipName in clipNames)
					{
						if (frameSkips.ContainsKey(clipName) == false)
							frameSkips.Add(clipName, globalBake);
						AnimationClip clip = clips.Find(q => q.name == clipName);
						int framesToBake = clip ? (int)(clip.length * fps / frameSkips[clipName]) : 0;
						GUILayout.BeginHorizontal();
						{
							bakeAnims[clipName] = EditorGUILayout.Toggle(string.Format("{0} ({1} frames)", clipName, framesToBake), bakeAnims[clipName]);
							GUI.enabled = bakeAnims[clipName];
							frameSkips[clipName] = Mathf.Clamp(EditorGUILayout.IntField(frameSkips[clipName]), 1, fps);
							GUI.enabled = true;
						}
						GUILayout.EndHorizontal();
						if (framesToBake > 500)
						{
							GUI.skin.label.richText = true;
							EditorGUILayout.LabelField("<color=red>Long animations degrade performance, consider using a higher frame skip value.</color>", GUI.skin.label);
						}
						if (bChange) frameSkips[clipName] = globalBake;
						if (frameSkips[clipName] != 1)
							modified = true;
					}
					EditorGUI.indentLevel--;
				}
				catch (System.Exception e)
				{
					Debug.LogError(e);
				}
				GUILayout.EndScrollView();
				if (modified)
					DrawText("Skipping more frames during baking will result in a smaller asset size, but potentially degrade animation quality.", Color.yellow);

				GUILayout.Space(10);
				int bakeCount = bakeAnims.Count(q => q.Value);
				GUI.enabled = bakeCount > 0;
				if (GUILayout.Button(string.Format("Generate Snapshots for {0} animation{1}", bakeCount, bakeCount > 1 ? "s" : string.Empty)))
                {
                    CreateSnapshots();
                }
				GUI.enabled = true;
				//SavePreferencesForAsset();
			}
			else // end if valid prefab
			{
				DrawText("Specify a asset to bake.", Color.red + Color.white * 0.5f);
			}
			EditorGUI.EndChangeCheck();
			if (GUI.changed)
            {
                Repaint();
            }
		}
		GUILayout.EndScrollView();
	}

    private void InitAnimationClipHashPath(AnimationClip clip)
    {
        positionPathHash.Clear();
        rotationPathHash.Clear();
        EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
        foreach (EditorCurveBinding item in curveBindings)
        {
            string path = item.path;
            string propertyName = item.propertyName;
            if (propertyName.Length > positionPath.Length)
            {
                if (propertyName.IndexOf(positionPath) >= 0)
                {
                    positionPathHash.Remove(path);
                    positionPathHash.Add(path);
                }
                else if (propertyName.IndexOf(rotationPath) >= 0)
                {
                    rotationPathHash.Remove(path);
                    rotationPathHash.Add(path);
                }
            }
        }
    }

    private void InitAnimationBones(GameObject animationTarget, GPUAnimation meshAnim)
    {
        bonesMap.Clear();
        Transform child = animationTarget.transform.Find("Bip01");
        joints = child.GetComponentsInChildren<Transform>();
        skeletonPoses = new Matrix4x4[joints.Length];
        bindSkeletonPoses = new Matrix4x4[joints.Length];
        //meshAnim.bones = new string[bones.Length];
        for (int i = 0; i < joints.Length; i++)
        {
            Transform bone = joints[i];
            bonesMap.Add(bone.name, i);
            skeletonPoses[i] = bone.transform.localToWorldMatrix;
            bindSkeletonPoses[i] = bone.transform.worldToLocalMatrix;
            //meshAnim.bones[i] = bone.name;
        }
    }

    private float GetAnimationClipCurve(AnimationClip clip, string path, string propertyName, float delta)
    {
        EditorCurveBinding curveBinding = EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName);
        AnimationCurve animationCurve = AnimationUtility.GetEditorCurve(clip, curveBinding);
        return animationCurve.Evaluate(delta);
    }

    private static float GetCurveValue(AnimationClip clip, string path, string prop, float time)
    {
        EditorCurveBinding binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), prop);
        return AnimationUtility.GetEditorCurve(clip, binding).Evaluate(time);
    }

    private void CreateSnapshots()
	{
		UnityEditor.Animations.AnimatorController bakeController = null;
        string assetPath = GetPrefabPath();
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("Mesh Animator", "Unable to locate the asset path for prefab: " + prefab.name, "OK");
            return;
        }

        HashSet<string> allAssets = new HashSet<string>();

        List<AnimationClip> clips = GetClips();
        foreach (var clip in clips)
        {
            allAssets.Add(AssetDatabase.GetAssetPath(clip));
        }

        string[] split = assetPath.Split("/".ToCharArray());

        string assetFolder = string.Empty;
        for (int s = 0; s < split.Length - 1; s++)
        {
            assetFolder += split[s] + "/";
        }

        var sampleGO = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity) as GameObject;
        if (meshFilters.Count(q => q) == 0 && skinnedRenderers.Count(q => q) == 0)
        {
            throw new System.Exception("Bake Error! No MeshFilter's or SkinnedMeshRender's found to bake!");
        }
        else
        {
            animator = sampleGO.GetComponent<Animator>();
            if (animator == null)
            {
                animator = sampleGO.GetComponentInChildren<Animator>();
            }

            InitAnimationBones(sampleGO, null);
            if (requiresAnimator)
            {
                bakeController = CreateBakeController();
                if (animator == null)
                {
                    animator = sampleGO.AddComponent<Animator>();
                    animator.runtimeAnimatorController = bakeController;
                    animator.avatar = GetAvatar();
                }
                else
                {
                    animator.runtimeAnimatorController = bakeController;
                    animator.avatar = GetAvatar();
                }
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = rootMotionMode == GPUAnimation.RootMotionMode.Baked;
            }

            GPUAnimationList meshAnimationList = ScriptableObject.CreateInstance<GPUAnimationList>();
            meshAnimationList.meshAnimations = new GPUAnimation[clips.Count];

            int startIndex = bonesMap.Count * 3;

            Transform rootMotionBaker = new GameObject().transform;
            for (int x = 0; x < clips.Count; x++)
            {
                AnimationClip animClip = clips[x];
                InitAnimationClipHashPath(animClip);

                if (bakeAnims.ContainsKey(animClip.name) && bakeAnims[animClip.name] == false) continue;
                if (frameSkips.ContainsKey(animClip.name) == false)
                {
                    Debug.LogWarningFormat("No animation with name {0} in frame skips", animClip.name);
                    continue;
                }
                string meshAnimationPath = string.Format("{0}{1}.asset", assetFolder, FormatClipName(animClip.name));
                GPUAnimation meshAnim = new GPUAnimation();
                meshAnim.length = animClip.length;

                int bakeFrames = Mathf.CeilToInt(animClip.length * fps);

                meshAnim.animationName = animClip.name;
                meshAnim.startIndex = startIndex;
                meshAnim.clipJoints = new Skeleton[bonesMap.Count];
                startIndex += bonesMap.Count * bakeFrames * 3;
                for (int i = 0; i < bonesMap.Count; i++)
                {
                    meshAnim.clipJoints[i] = new Skeleton();
                    meshAnim.clipJoints[i].jontFrameMatrixs = new Matrix4x4[bakeFrames];
                }

                if (animClip.isLooping)
                {
                    meshAnim.wrapMode = WrapMode.Loop;
                }
                else
                {
                    meshAnim.wrapMode = animClip.wrapMode;
                }
                meshAnim.frameSkip = frameSkips[animClip.name];

                meshAnimationList.meshAnimations[x] = meshAnim;
            }

            int textureSize = 2;
            while (textureSize * textureSize < startIndex)
            {
                textureSize = textureSize << 1;
            }

            Texture2D meshTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBAHalf, false, true);
            meshTexture.filterMode = FilterMode.Point;

            Color[] meshTexturePixels = meshTexture.GetPixels();
            Matrix4x4 matrix = Matrix4x4.identity;
            int pixelIndex = 0;
            for (int i = 0; i < bonesMap.Count; i++)
            {
                meshTexturePixels[pixelIndex] = new Color(matrix.m00, matrix.m01, matrix.m02, matrix.m03);
                pixelIndex++;
                meshTexturePixels[pixelIndex] = new Color(matrix.m10, matrix.m11, matrix.m12, matrix.m13);
                pixelIndex++;
                meshTexturePixels[pixelIndex] = new Color(matrix.m20, matrix.m21, matrix.m22, matrix.m23);
                pixelIndex++;
            }

            int animCount = 0;
            for (int j = 0; j < clips.Count; j++)
            {
                AnimationClip animClip = clips[j];
                GPUAnimation meshAnim = meshAnimationList.meshAnimations[j];

                int bakeFrames = Mathf.CeilToInt(animClip.length * fps);
                meshAnim.totalFrames = bakeFrames;
                int frame = 0;
                for (int i = 0; i <= bakeFrames; i += frameSkips[animClip.name])
                {
                    float bakeDelta = Mathf.Clamp01(((float)i / bakeFrames));
                    EditorUtility.DisplayProgressBar("Baking Animation", string.Format("Processing: {0} Frame: {1}", animClip.name, i), bakeDelta);
                    float animationTime = bakeDelta * animClip.length;

                    foreach (string path in positionPathHash)
                    {
                        string boneName = path.Substring(path.LastIndexOf("/") + 1);
                        if (bonesMap.ContainsKey(boneName))
                        {
                            Transform child = joints[bonesMap[boneName]];
                            float postionX = GetAnimationClipCurve(animClip, path, positionPath + ".x", bakeDelta);
                            float postionY = GetAnimationClipCurve(animClip, path, positionPath + ".y", bakeDelta);
                            float postionZ = GetAnimationClipCurve(animClip, path, positionPath + ".z", bakeDelta);
                            child.localPosition = new Vector3(postionX, postionY, postionZ);
                        }
                    }

                    foreach (string path in rotationPathHash)
                    {
                        string boneName = path.Substring(path.LastIndexOf("/") + 1);
                        if (bonesMap.ContainsKey(boneName))
                        {
                            Transform child = joints[bonesMap[boneName]];
                            float rotationX = GetAnimationClipCurve(animClip, path, rotationPath + ".x", bakeDelta);
                            float rotationY = GetAnimationClipCurve(animClip, path, rotationPath + ".y", bakeDelta);
                            float rotationZ = GetAnimationClipCurve(animClip, path, rotationPath + ".z", bakeDelta);
                            float rotationW = GetAnimationClipCurve(animClip, path, rotationPath + ".w", bakeDelta);
                            Quaternion rotation = new Quaternion(rotationX, rotationY, rotationZ, rotationW);
                            float r = rotationX * rotationX + rotationY * rotationY + rotationZ * rotationZ + rotationW * rotationW;
                            if (r >= .1f)
                            {
                                r = 1.0f / Mathf.Sqrt(r);
                                rotation.x *= r;
                                rotation.y *= r;
                                rotation.z *= r;
                                rotation.w *= r;
                            }

                            child.localRotation = rotation;
                        }
                    }

                    for (int k = 0; k < bonesMap.Count; k++)
                    {
                        Transform child = joints[k];

                        matrix = child.transform.localToWorldMatrix;
                        meshAnim.clipJoints[k].jontFrameMatrixs[k] = matrix;
                        matrix = matrix * bindSkeletonPoses[k];
                        meshTexturePixels[pixelIndex] = new Color(matrix.m00, matrix.m01, matrix.m02, matrix.m03);
                        pixelIndex++;
                        meshTexturePixels[pixelIndex] = new Color(matrix.m10, matrix.m11, matrix.m12, matrix.m13);
                        pixelIndex++;
                        meshTexturePixels[pixelIndex] = new Color(matrix.m20, matrix.m21, matrix.m22, matrix.m23);
                        pixelIndex++;
                    }

                    frame++;
                }
                meshAnim.meshTexturePixels = meshTexturePixels;
                animCount++;
            }

            meshTexture.SetPixels(meshTexturePixels);
            meshTexture.Apply();
            AssetDatabase.CreateAsset(meshTexture, assetFolder + "_AnimationTexture.asset");
            meshAnimationList.totalJoints = bonesMap.Count;
            meshAnimationList.skinningTexSize = textureSize;

            AssetDatabase.CreateAsset(meshAnimationList, assetFolder + "_AnimationConfig.asset");

            SkinnedMeshRenderer skinnedMeshRenderer = sampleGO.GetComponentInChildren<SkinnedMeshRenderer>();
            Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;
            Transform[] transforms = skinnedMeshRenderer.bones;
            int vertexCount = sharedMesh.vertexCount;
            List<Vector4> indices = new List<Vector4>();
            List<Vector4> weights = new List<Vector4>();
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];

            Matrix4x4 meshMatrix = skeletonPoses[bonesMap[transforms[0].name]] * sharedMesh.bindposes[0];
            BoneWeight[] boneWeights = sharedMesh.boneWeights;
            for (int v = 0; v < vertexCount; v++)
            {
                BoneWeight weight = boneWeights[v];
                float weight0 = weight.weight0;
                float weight1 = weight.weight1;
                float weight2 = weight.weight2;
                float weight3 = weight.weight3;
                int boneIndex0 = bonesMap[transforms[weight.boneIndex0].name];
                int boneIndex1 = bonesMap[transforms[weight.boneIndex1].name];
                int boneIndex2 = bonesMap[transforms[weight.boneIndex2].name];
                int boneIndex3 = bonesMap[transforms[weight.boneIndex3].name];
                indices.Add(new Vector4(boneIndex0, boneIndex1, boneIndex2, boneIndex3));
                weights.Add(new Vector4(weight0, weight1, weight2, weight3));
                vertices[v] = meshMatrix * sharedMesh.vertices[v];
                normals[v] = meshMatrix * sharedMesh.normals[v];
                weight.boneIndex0 = boneIndex0;
                weight.boneIndex1 = boneIndex1;
                weight.boneIndex2 = boneIndex2;
                weight.boneIndex3 = boneIndex3;
                boneWeights[v] = weight;
            }

            Mesh newMesh = new Mesh();
            newMesh.vertices = vertices;
            newMesh.normals = normals;
            newMesh.triangles = sharedMesh.triangles;
            newMesh.uv = sharedMesh.uv;
            newMesh.SetUVs(1, indices);
            newMesh.SetUVs(2, weights);
            skinnedMeshRenderer.bones = joints;
            skinnedMeshRenderer.sharedMesh = newMesh;
            AssetDatabase.CreateAsset(newMesh, assetFolder + "_" + sharedMesh.name + ".asset");
        }
        GameObject.DestroyImmediate(sampleGO);
        EditorUtility.ClearProgressBar();
        EditorUtility.DisplayDialog("GPU Animator", string.Format("Baked {0} animation{1} successfully!", clips.Count
            , clips.Count > 1 ? "s" : string.Empty), "OK");
        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(bakeController));
	}
	private Avatar GetAvatar()
	{
		if (animAvatar)
			return animAvatar;
		var objs = EditorUtility.CollectDependencies(new Object[] { prefab }).ToList();
		foreach (var obj in objs.ToArray())
			objs.AddRange(AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(obj)));
		objs.RemoveAll(q => q is Avatar == false || q == null);
		if (objs.Count > 0)
			animAvatar = objs[0] as Avatar;
		return animAvatar;
	}
	private List<AnimationClip> GetClips()
	{
		var clips = EditorUtility.CollectDependencies(new Object[] { prefab }).ToList();
		foreach (var obj in clips.ToArray())
			clips.AddRange(AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(obj)));
		clips.AddRange(customClips.Select(q => (Object)q));
		clips.RemoveAll(q => q is AnimationClip == false || q == null);
		foreach (AnimationClip clip in clips)
		{
			if (bakeAnims.ContainsKey(clip.name) == false)
				bakeAnims.Add(clip.name, true);
		}
		clips.RemoveAll(q => bakeAnims.ContainsKey(q.name) == false);
		clips.RemoveAll(q => bakeAnims[q.name] == false);

		var distinctClips = clips.Select(q => (AnimationClip)q).Distinct().ToList();
		requiresAnimator = false;
		var humanoidCheck = new List<AnimationClip>(distinctClips);
		if (animController)
		{
			humanoidCheck.AddRange(animController.animationClips);
			distinctClips.AddRange(animController.animationClips);
			distinctClips = distinctClips.Distinct().ToList();
		}
		foreach (var c in humanoidCheck)
			if (c && c.isHumanMotion)
				requiresAnimator = true;
		try
		{
			if (requiresAnimator == false)
			{
				var importer = GetImporter(GetPrefabPath());
				if (importer && importer.animationType == ModelImporterAnimationType.Human)
				{
					requiresAnimator = true;
				}
			}
		}
		catch { }
		try
		{
			if (requiresAnimator == false && IsOptimizedAnimator())
				requiresAnimator = true;
		}
		catch { }
		for (int i = 0; i < distinctClips.Count; i++)
		{
			if (bakeAnims.ContainsKey(distinctClips[i].name) == false)
				bakeAnims.Add(distinctClips[i].name, true);
		}
		return distinctClips;
	}
	private void DrawText(string text, Color color)
	{
		GUI.contentColor = color;
		GUILayout.TextArea(text);
		GUI.contentColor = oColor;
	}
	private string GetPrefabPath()
	{
		string assetPath = AssetDatabase.GetAssetPath(prefab);
		if (string.IsNullOrEmpty(assetPath))
		{
			Object parentObject = PrefabUtility.GetPrefabParent(prefab);
			assetPath = AssetDatabase.GetAssetPath(parentObject);
		}
		return assetPath;
	}

	private void OnPrefabChanged()
	{
		if (spawnedAsset)
			GameObject.DestroyImmediate(spawnedAsset.gameObject);
		if (Application.isPlaying)
		{
			return;
		}
		animator = null;
		animAvatar = null;
		if (prefab)
		{
			if (spawnedAsset == null)
			{
				spawnedAsset = GameObject.Instantiate(prefab) as GameObject;
				SetChildFlags(spawnedAsset.transform, HideFlags.HideAndDontSave);
			}
			bakeAnims.Clear();
			frameSkips.Clear();
			AutoPopulateFiltersAndRenderers();
			AutoPopulateAnimatorAndController();

			//LoadPreferencesForAsset();
		}
		previousPrefab = prefab;
	}

	private void AutoPopulateFiltersAndRenderers()
	{
		meshFilters.Clear();
		skinnedRenderers.Clear();
		MeshFilter[] filtersInPrefab = spawnedAsset.GetComponentsInChildren<MeshFilter>();
		for (int i = 0; i < filtersInPrefab.Length; i++)
		{
			if (meshFilters.Contains(filtersInPrefab[i]) == false)
				meshFilters.Add(filtersInPrefab[i]);
			if (filtersInPrefab[i].GetComponent<MeshRenderer>())
				filtersInPrefab[i].GetComponent<MeshRenderer>().enabled = false;
		}
		SkinnedMeshRenderer[] renderers = spawnedAsset.GetComponentsInChildren<SkinnedMeshRenderer>();
		for (int i = 0; i < renderers.Length; i++)
		{
			if (skinnedRenderers.Contains(renderers[i]) == false)
				skinnedRenderers.Add(renderers[i]);
			renderers[i].enabled = false;
		}
	}
	private void AutoPopulateAnimatorAndController()
	{
		animator = spawnedAsset.GetComponent<Animator>();
		if (animator == null)
			animator = spawnedAsset.GetComponentInChildren<Animator>();
		if (animator && animController == null)
			animController = animator.runtimeAnimatorController;
	}
	private bool IsOptimizedAnimator()
	{
		var i = GetAllImporters();
		if (i.Count > 0)
			return i.Any(q => q.optimizeGameObjects);
		return false;
	}

	private ModelImporter GetImporter(string p)
	{
		return ModelImporter.GetAtPath(p) as ModelImporter;
	}

	private List<ModelImporter> GetAllImporters()
	{
		List<ModelImporter> importers = new List<ModelImporter>();
		importers.Add(GetImporter(GetPrefabPath()));
		foreach (var mf in meshFilters)
		{
			if (mf && mf.sharedMesh)
			{
				importers.Add(GetImporter(AssetDatabase.GetAssetPath(mf.sharedMesh)));
			}
		}
		foreach (var sr in skinnedRenderers)
		{
			if (sr && sr.sharedMesh)
			{
				importers.Add(GetImporter(AssetDatabase.GetAssetPath(sr.sharedMesh)));
			}
		}
		importers.RemoveAll(q => q == null);
		importers = importers.Distinct().ToList();
		return importers;
	}

	private void SetChildFlags(Transform t, HideFlags flags)
	{
		Queue<Transform> q = new Queue<Transform>();
		q.Enqueue(t);
		for (int i = 0; i < t.childCount; i++)
		{
			Transform c = t.GetChild(i);
			q.Enqueue(c);
			SetChildFlags(c, flags);
		}
		while (q.Count > 0)
		{
			q.Dequeue().gameObject.hideFlags = flags;
		}
	}

	private UnityEditor.Animations.AnimatorController CreateBakeController()
	{
		// Creates the controller automatically containing all animation clips
		string tempPath = "Assets/TempBakeController.controller";
		var bakeName = AssetDatabase.GenerateUniqueAssetPath(tempPath);
		var controller = AnimatorController.CreateAnimatorControllerAtPath(bakeName);
		var baseStateMachine = controller.layers[0].stateMachine;
		var clips = GetClips();
		foreach (var clip in clips)
		{
			var state = baseStateMachine.AddState(clip.name);
			state.motion = clip;
		}
		return controller;
	}

	private string FormatClipName(string name)
	{
		string badChars = "!@#$%%^&*()=+}{[]'\";:|";
		for (int i = 0; i < badChars.Length; i++)
		{
			name = name.Replace(badChars[i], '_');
		}
		return name;
	}
}
