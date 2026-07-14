# SkinnedMeshBaker

一个 Unity 编辑器工具，用于将**蒙皮网格渲染器（SkinnedMeshRenderer）** 烘焙为MeshRenderer，生成可复用的 Prefab。
使其使用的模型保持其姿态，并降低渲染负载。接收Bakery的预烘焙光照。此外，还可阻止从世界数据中提取其模型数据。

### 特别致谢 / Special Thanks

[SkinnedMeshBaker(アバターポーズ固定するやつ) - バーチャルマーケット](https://vket.com/docs/submission_tips_skinned_mesh_baker)
此链接为Vket官方提供的SkinnedMeshBaker（曾用名AvatarPoseBaker），在我所需要时提供了莫大的帮助。但与此同时程序中针对Bones部分为0时无法正确处理，所以重新编写了此工具。

## 使用方法

1. **导入至 Unity 编辑器**
   - 从Releases中下载并打开SkinnedMeshBaker.unitypackage

2. **选择目标对象**
   - 在 Hierarchy 面板中选中要烘焙的 GameObject
   - 该对象包含 SkinnedMeshRenderer 

3. **执行烘焙**
   - 右键点击 GameObject → `GameObject` → `Bake Skinned Mesh`
   - 或从菜单栏 `GameObject` → `Bake Skinned Mesh`

4. **查看结果**
   - 烘焙的 Mesh 资源保存到：`Assets/BakeResult/Baked_[ObjectName]/`
   - 自动生成 Prefab：`Baked_[ObjectName].prefab`
   - 同时在Hierarchy中生成同样空间位置、同样缩放比的Baked File

## 工作原理

### 烘焙流程

```
源 GameObject (含 SkinnedMeshRenderer)
         ↓
[递归遍历层级]
         ↓
情况 A: SkinnedMeshRenderer
  ├─ 烘焙为 Mesh 资源 (.asset)
  ├─ 创建 MeshFilter + MeshRenderer
  ├─ 复制渲染属性
  └─ 保留 localScale

情况 B: 普通 MeshRenderer
  ├─ 复制 Mesh 引用
  ├─ 复制渲染器属性
  └─ 保留 localScale

情况 C: 中间层级 (无网格)
  ├─ 保留层级结构
  └─ 剔除无用骨骼链
         ↓
[生成 Prefab]
         ↓
Assets/BakeResult/Baked_[Name]/Baked_[Name].prefab
```

### 技术细节

#### 1. **BakeMesh 缩放处理**
- `SkinnedMeshRenderer.BakeMesh(mesh, useScale: true)` 会将根骨骼链的缩放烤入顶点
- **不包括** SkinnedMeshRenderer 自身的 localScale
- 因此必须在目标对象上保留原始 localScale，避免变形

#### 2. **网格中心化**
- 自动计算网格边界框中心
- 调整顶点位置，使网格中心位于原点
- 通过 Transform 世界位置补偿，保证视觉位置不变
- 提高后续处理的精度

#### 3. **智能骨骼过滤**
- 深度检查每个节点是否在其子树中包含渲染器
- 自动剔除纯骨骼链（无网格的分支）
- 保留必要的层级结构，减少无用节点

#### 4. **渲染属性复制**
复制的属性包括：
- `sharedMaterials` - 材质列表
- `shadowCastingMode` - 阴影投射模式
- `receiveShadows` - 接收阴影
- `lightProbeUsage` - 光探针用法
- `reflectionProbeUsage` - 反射探针用法
- `probeAnchor` - 探针锚点
- `motionVectorGenerationMode` - 运动向量生成模式
- `allowOcclusionWhenDynamic` - 动态遮挡
- `renderingLayerMask` - 渲染层遮罩

## 输出结构

烘焙后生成的资源组织如下：

```
Assets/BakeResult/
└── Baked_[ObjectName]/
    ├── [ObjectName]_BakedMesh_0.asset    # 烘焙的网格资源
    ├── [ObjectName]_BakedMesh_1.asset    # （可能有多个）
    └── Baked_[ObjectName].prefab         # 最终 Prefab
```

## 应用场景

**适用于：**
- 骨骼动画模型 → 静态模型转换
- 性能优化：移除不需要的骨骼计算
- 角色动作定帧烘焙
- 预制件库生成

## 技术要求

- **Unity 版本**：2022.3.22f1 LTS 或更高
- **脚本语言**：C#
- **编辑器工具**：仅在编辑器中可用


## 示例

**原始层级：**
```
Character (GameObject)
├── Armature (Bone)
│   ├── Bone.001
│   ├── Bone.002
│   └── BodyMesh (SkinnedMeshRenderer)
├── Hair (SkinnedMeshRenderer)
└── Accessories (MeshRenderer)
```

**烘焙后：**
```
Baked_Character
├── Armature (保留中间层级)
│   └── BodyMesh (MeshFilter + MeshRenderer)
├── Hair (MeshFilter + MeshRenderer)
└── Accessories (MeshFilter + MeshRenderer)
```

*注意：无网格的 Bone.001 和 Bone.002 被自动剔除*

## 许可

MIT License

Copyright (c) 2026 xxKawa

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
