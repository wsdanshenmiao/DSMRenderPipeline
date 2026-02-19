using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework.Internal;
using UniHumanoid;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DSM
{
    public static class BoundsExtension
    {
        public static float Area(this Bounds bounds)
        {
            Vector3 size = bounds.size;
            return (size.x * size.y + size.y * size.z + size.z * size.x) * 2;
        }

        public static bool Contains(this Bounds a, Bounds b)
        {
            return a.Contains(b.min) && a.Contains(b.max);
        }

        public static Bounds Union(this Bounds a, Bounds b)
        {
            Bounds result = new Bounds(a.center, a.size);
            result.Encapsulate(b);
            return result;
        }
    }

    public class BoundingBoxManager : MonoBehaviour
    {
        private BVHTree m_BVH;
        private Dictionary<Renderer, BVHTree.BVHNode> m_BVHMap;

        private void BuildSceneBounds()
        {
            List<Renderer> worldRenderer = new();
            
            // 获取场景中所有物体的包围盒
            for(int i = 0; i < SceneManager.sceneCount; ++i)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach(var gameObj in rootObjects)
                {
                    Renderer[] renderers = gameObj.GetComponentsInChildren<Renderer>();
                    worldRenderer.AddRange(renderers);
                }
            }

            // 为包围盒构建层次包围盒
            m_BVH = new BVHTree(worldRenderer);
        }

        private void Update()
        {
            if (m_BVH == null)
                return;

            List<Renderer> worldRenderer = new();
            // 判断场景中是否有物体被移动或者缩放，如果有则更新对应的包围盒
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach(var gameObj in rootObjects)
                {
                    Renderer[] renderers = gameObj.GetComponentsInChildren<Renderer>();
                    worldRenderer.AddRange(renderers);
                }
            }

            var leafNodes = m_BVH.GetLeafNodes().ToLookup(a => a.renderer);
            // 用存储新的BVH节点，方便后续删除已经被销毁的物体的包围盒节点
            var newBVHMap = leafNodes.ToDictionary(a => a.Key, a => a.ToList());
            foreach (var renderer in worldRenderer)
            {
                if (!leafNodes.Contains(renderer))
                {
                    m_BVH.InsertNode(renderer);
                }
                else
                {
                    foreach(var node in leafNodes[renderer])
                    {
                        if(node.bounds != renderer.bounds)
                        {
                            m_BVH.RemoveNode(node);
                            m_BVH.InsertNode(renderer);
                        }
                    }
                    newBVHMap.Remove(renderer);
                }
            }

            // 删除已经被销毁的物体的包围盒节点
            foreach(var group in newBVHMap)
            {
                foreach(var node in group.Value)
                {
                    m_BVH.RemoveNode(node);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if(m_BVH == null)
            {
                BuildSceneBounds();
            }

            Stack<Tuple<BVHTree.BVHNode, int>> stack = new();
            stack.Push(new Tuple<BVHTree.BVHNode, int>(m_BVH.GetRoot(), 0));
            while(stack.Count > 0)
            {
                var tuple = stack.Pop();
                BVHTree.BVHNode node = tuple.Item1;
                int depth = tuple.Item2;
                if(node == null)
                    continue;

                // 根据深度设置颜色（例如从绿到红渐变）
                float hue = Mathf.Repeat(depth * 0.1f, 1f); // 每层改变色相
                Gizmos.color = Color.HSVToRGB(hue, 1f, 1f);
                Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
                if(node.left != null || node.right != null)
                {
                    stack.Push(new Tuple<BVHTree.BVHNode, int>(node.left, depth + 1));
                    stack.Push(new Tuple<BVHTree.BVHNode, int>(node.right, depth + 1));
                }
            }
        }
    }
}
