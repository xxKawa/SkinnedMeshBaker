// Created by xxKawa
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// 编辑器工具：将选中的 GameObject 层级中的 SkinnedMeshRenderer 烘焙为静态 Mesh，
    /// 并生成一个可复用的 Prefab。保留渲染器设置、材质和局部变换，同时剔除不含渲染器的骨骼子树。
    /// </summary>
    public static class SkinnedMeshBaker
    {
        private const string RootFolder = "Assets/BakeResult";

        /// <summary>
        /// 对当前选中的 GameObject（及其子层级）执行烘焙操作：
        /// - 将 SkinnedMeshRenderer 烘焙为 Mesh 资源并保存到指定 Assets 子目录。
        /// - 复制渲染器属性与材质，保留原始局部变换。
        /// - 生成一个 Prefab 并静默保存到 AssetDatabase。
        /// 如果未选中对象，会在控制台输出一条警告。
        /// </summary>
        [MenuItem("GameObject/Bake Skinned Mesh", false, 10)]
        public static void BakeSelectedSkinnedMesh()
        {
            GameObject sourceRoot = Selection.activeGameObject;
            if (sourceRoot == null)
            {
                Debug.LogWarning("[SkinnedMeshBaker] 请在 Hierarchy 中选择一个目标 GameObject。");
                return;
            }

            // 1. 准备目标文件夹
            string targetFolderName = $"Baked_{sourceRoot.name}";
            string folderPath = EnsureFolder(RootFolder, targetFolderName);

            // 2. 创建新的 Root GameObject
            GameObject bakedRoot = new GameObject(targetFolderName);
            CopyLocalTransformAndTag(sourceRoot.transform, bakedRoot.transform, sourceRoot.transform.parent);

            // 3. 递归处理层级并烘焙 Mesh
            int meshIndex = 0;
            ProcessNodeRecursive(sourceRoot.transform, bakedRoot.transform, folderPath, ref meshIndex);

            // 4. 保存为 Prefab
            string prefabPath = $"{folderPath}/{targetFolderName}.prefab";
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(bakedRoot, prefabPath);

            // 5. 刷新资源数据库 (静默保存，绝对不弹跳干扰用户当前的 Project 目录视图)
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = bakedRoot;
            Debug.Log($"[SkinnedMeshBaker] 烘焙完成！Prefab 已静默保存至: {prefabPath}");
        }

        /// <summary>
        /// 菜单项校验：仅当用户在层级中选中对象时启用菜单项。
        /// </summary>
        [MenuItem("GameObject/Bake Skinned Mesh", true)]
        private static bool ValidateBakeSelectedSkinnedMesh()
        {
            return Selection.activeGameObject != null;
        }

        /// <summary>
        /// 递归遍历源层级并在目标层级重建需要的节点。
        /// 对于 <see cref="SkinnedMeshRenderer"/>：烘焙为新的 <see cref="Mesh"/> 资源并创建对应的 <see cref="MeshFilter"/>+<see cref="MeshRenderer"/>。
        /// 对于普通 <see cref="MeshRenderer"/>：直接复制 Mesh 引用并复制渲染属性。
        /// 会智能跳过在其自身或任何后代中都不包含渲染器的骨骼空节点，从而避免生成无用的骨骼层级。
        /// </summary>
        /// <param name="source">源 Transform（来自原始模型层级）</param>
        /// <param name="target">目标 Transform（在烘焙生成的新层级中）</param>
        /// <param name="folderPath">用于保存生成 Mesh 资源的 Asset 子目录路径</param>
        /// <param name="meshIndex">用于为生成的 Mesh 命名的序号（通过 ref 传递）</param>
        private static void ProcessNodeRecursive(Transform source, Transform target, string folderPath, ref int meshIndex)
        {
            target.gameObject.layer = source.gameObject.layer;
            target.gameObject.tag = source.gameObject.tag;

            // 情况 A：处理 SkinnedMeshRenderer（骨骼网格）
            if (source.TryGetComponent<SkinnedMeshRenderer>(out var smr))
            {
                Mesh bakedMesh = new Mesh
            {
                name = $"{source.name}_BakedMesh_{meshIndex++}"
            };
            // BakeMesh 参数说明：useScale = true 表示会将 rootBone 链的缩放信息烤入顶点。
            // 注意：BakeMesh 并不会把 SkinnedMeshRenderer 自身 Transform 的 localScale 烤入顶点，
            // 因此必须在目标对象上保留原始的 localScale，以避免缩放失真。
            smr.BakeMesh(bakedMesh, true);
            bakedMesh.RecalculateBounds();

            string meshAssetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{bakedMesh.name}.asset");
            AssetDatabase.CreateAsset(bakedMesh, meshAssetPath);

            var mf = target.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = bakedMesh;
+
            var mr = target.gameObject.AddComponent<MeshRenderer>();
            CopyRendererProperties(smr, mr);

            // 保留源对象的局部缩放，避免 BakeMesh 未烤入的缩放丢失导致变形
            target.localScale = source.localScale;
            
            // 可选：将网格中心重心化并用 Transform 补偿位置（已保留为示例，默认启用）
            Vector3 localCenter = bakedMesh.bounds.center;
            Vector3[] verts = bakedMesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] -= localCenter;
            }
            bakedMesh.vertices = verts;
            bakedMesh.RecalculateBounds();

            // 把减掉的局部偏移通过目标 Transform 的世界位置补偿，保证视觉位置不变
            target.position += target.TransformVector(localCenter);
               }
            // 情况 B：普通的 MeshRenderer (如道具/饰品)
            else if (source.TryGetComponent<MeshRenderer>(out var mr) && source.TryGetComponent<MeshFilter>(out var mf))
            {
                var newMf = target.gameObject.AddComponent<MeshFilter>();
                newMf.sharedMesh = mf.sharedMesh;

                var newMr = target.gameObject.AddComponent<MeshRenderer>();
                CopyRendererProperties(mr, newMr);
                
                // 普通 Mesh 没有将 Scale 烤进顶点，所以保持继承原 Scale
                target.localScale = source.localScale;
            }
            else
            {
                // 如果只是中间层级文件夹容器（既不是SMR也不是MR），正常继承其 Scale
                target.localScale = source.localScale;
            }

            // 递归处理子节点
            for (int i = 0; i < source.childCount; i++)
            {
                Transform childSource = source.GetChild(i);

                // 【核心修复 3】智能过滤剔除源骨骼层级！
                // 如果这个子节点本身没有网格，它的任何子孙后代也全都没有网格（比如 Armature 下的纯骨骼链），
                // 那么它就是彻底无效的源骨骼或垃圾空节点，直接放弃创建，从层级树里剔除！
                if (!HasAnyRendererInTree(childSource))
                {
                    continue;
                }

                GameObject childTargetGO = new GameObject(childSource.name);
                Transform childTarget = childTargetGO.transform;

                // 严格使用 SetParent(..., false) 建立父子关系并同步位置与旋转
                childTarget.SetParent(target, false);
                childTarget.localPosition = childSource.localPosition;
                childTarget.localRotation = childSource.localRotation;

                ProcessNodeRecursive(childSource, childTarget, folderPath, ref meshIndex);
            }
        }

        /// <summary>
        /// 深度检查函数：判断一个节点自身或其任何子孙后代中，是否包含有效的渲染器 (SMR 或 MR)。
        /// 用于精准区分“要保留的层级文件夹”和“要自动剔除的源骨骼”。
        /// </summary>
        private static bool HasAnyRendererInTree(Transform node)
        {
            if (node.GetComponent<SkinnedMeshRenderer>() != null || node.GetComponent<MeshRenderer>() != null)
            {
                return true;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                if (HasAnyRendererInTree(node.GetChild(i)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 复制 Renderer 的所有核心光照与渲染属性
        /// </summary>
        private static void CopyRendererProperties(Renderer source, Renderer target)
        {
            target.sharedMaterials = source.sharedMaterials;
            target.shadowCastingMode = source.shadowCastingMode;
            target.receiveShadows = source.receiveShadows;
            target.lightProbeUsage = source.lightProbeUsage;
            target.reflectionProbeUsage = source.reflectionProbeUsage;
            target.probeAnchor = source.probeAnchor;
            target.motionVectorGenerationMode = source.motionVectorGenerationMode;
            target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            target.renderingLayerMask = source.renderingLayerMask;
        }

        /// <summary>
        /// 严格遵循 Local Transform 复制，避免受到父级缩放的污染
        /// </summary>
        private static void CopyLocalTransformAndTag(Transform source, Transform target, Transform parent)
        {
            target.SetParent(parent, false);
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        /// <summary>
        /// 确保目标文件夹存在
        /// </summary>
        private static string EnsureFolder(string rootPath, string subFolderName)
        {
            if (!AssetDatabase.IsValidFolder(rootPath))
            {
                AssetDatabase.CreateFolder("Assets", "BakeResult");
            }

            string fullPath = $"{rootPath}/{subFolderName}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(rootPath, subFolderName);
            }

            return fullPath;
        }
    }
}