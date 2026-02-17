using System;
using System.Collections.Generic;
using NUnit.Framework.Internal;
using UniHumanoid;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DSM
{
    public class BoundingBoxManager : MonoBehaviour
    {
        private BVH m_BVH;
        private Dictionary<Renderer, BVH> m_BVHMap;

        private void BuildSceneBounds()
        {
            List<Renderer> worldRenderer = new();
            m_BVHMap = new();
            
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
            m_BVH = new BVH(worldRenderer);

            // 构建物体到BVH节点的映射
            Stack<BVH> stack = new();
            stack.Push(m_BVH);
            while(stack.Count > 0)
            {
                BVH node = stack.Pop();
                if(node == null)
                    continue;

                if(node.renderer != null)
                {
                    m_BVHMap[node.renderer] = node;
                }
                if(node.left != null || node.right != null)
                {
                    stack.Push(node.left);
                    stack.Push(node.right);
                }
            }
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

            Dictionary<Renderer, BVH> newBVHMap = new();
            foreach (var renderer in worldRenderer)
            {
                if (!m_BVHMap.ContainsKey(renderer))
                {
                    // 插入新物体的包围盒
                    newBVHMap[renderer] = m_BVH.InsertNode(renderer);
                }
                else if(renderer.bounds != m_BVHMap[renderer].bounds)
                {
                    // 更新节点的包围盒
                    // 删除映射表中的节点以便最后删除移除的物品的节点
                    m_BVHMap.Remove(renderer);
                    m_BVH.RemoveNode(renderer);
                    newBVHMap[renderer] = m_BVH.InsertNode(renderer);
                }
                else
                {
                    newBVHMap[renderer] = m_BVHMap[renderer];
                    m_BVHMap.Remove(renderer);
                }
            }

            // 删除移除的物品的节点
            foreach(var node in m_BVHMap.Values)
            {
                m_BVH.RemoveNode(node);
            }

            m_BVHMap = newBVHMap;
        }

        private void OnDrawGizmos()
        {
            if(m_BVH == null)
            {
                BuildSceneBounds();
            }

            Stack<Tuple<BVH, int>> stack = new();
            stack.Push(new Tuple<BVH, int>(m_BVH, 0));
            while(stack.Count > 0)
            {
                var tuple = stack.Pop();
                BVH node = tuple.Item1;
                int depth = tuple.Item2;
                if(node == null)
                    continue;

                // 根据深度设置颜色（例如从绿到红渐变）
                float hue = Mathf.Repeat(depth * 0.1f, 1f); // 每层改变色相
                Gizmos.color = Color.HSVToRGB(hue, 1f, 1f);
                Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
                if(node.left != null || node.right != null)
                {
                    stack.Push(new Tuple<BVH, int>(node.left, depth + 1));
                    stack.Push(new Tuple<BVH, int>(node.right, depth + 1));
                }
            }
        }
    }
}
