using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DSM
{    
    public class BVHTree
    {
        public class BVHNode
        {
            public Bounds bounds;
            public Renderer renderer;
            public BVHNode left;
            public BVHNode right;
            public BVHNode parent;
            
            public BVHNode() { }
            public BVHNode(BVHNode other)
            {
                bounds = other.bounds;
                renderer = other.renderer;
                left = other.left;
                right = other.right;
                parent = other.parent;
            }

            public void UpdateBounds()
            {
                if (left != null && right != null)
                {
                    bounds = left.bounds;
                    bounds.Encapsulate(right.bounds);
                }
                // 更新父节点的包围盒
                parent?.UpdateBounds();
            }
        }

        private BVHNode m_Root = null;
        private List<BVHNode> m_LeafNodes = new();

        public BVHTree(List<Renderer> renderers)
        {
            if (renderers.Count <= 0)
                return;

            foreach(var renderer in renderers)
            {
                InsertNode(renderer);
            }
        }

        public BVHNode GetRoot()
        {
            return m_Root;
        }

        public List<BVHNode> GetLeafNodes()
        {
            return m_LeafNodes;
        }

        public BVHNode FindNode(Renderer renderer)
        {
            if (renderer == null || m_Root == null)
                return null;

            Stack<BVHNode> stack = new();
            stack.Push(m_Root);
            while(stack.Count > 0)
            {
                BVHNode node = stack.Pop();
                if(node == null)
                    continue;
                if (node.renderer == renderer)
                    return node;

                if(node.left != null && node.right != null)
                {
                    stack.Push(node.left);
                    stack.Push(node.right);
                }
            }

            return null;
        }

        public BVHNode InsertNode(Renderer renderer)
        {
            if (renderer == null)
                return null;

            // 搜索合适的插入位置
            BVHNode sibling = FindBestNode(renderer);
            if (sibling == null)
            {
                m_Root = new BVHNode();
                m_Root.renderer = renderer;
                m_Root.bounds = renderer.bounds;
                m_LeafNodes.Add(m_Root);
                return m_Root;
            }

            // 创建新的父节点并插入新的节点
            BVHNode newNode = new BVHNode();
            newNode.renderer = renderer;
            newNode.bounds = renderer.bounds;

            BVHNode oldParent = sibling.parent;
            BVHNode newParent = new BVHNode();
            newParent.parent = oldParent;

            if (oldParent != null)
            {
                // 更新父节点的子节点
                if (oldParent.left == sibling)
                    oldParent.left = newParent;
                else
                    oldParent.right = newParent;
            }
            else
            {
                m_Root = newParent;
            }

            newParent.left = sibling;
            newParent.right = newNode;
            sibling.parent = newParent;
            newNode.parent = newParent;

            // 更新插入节点的祖先节点
            newNode.UpdateBounds();

            // 将叶子节点插入到链表中
            m_LeafNodes.Add(newNode);
            return newNode;
        }

        public void RemoveNode(BVHNode node)
        {
            if(node == null)
                return;

            Assert.IsNull(node.left);
            Assert.IsNull(node.right);

            var nodeParent = node.parent;
            if (nodeParent == null)
            {
                m_Root = null;
                return;
            }

            Assert.IsTrue(nodeParent.left == node || nodeParent.right == node);
            BVHNode otherChild = nodeParent.left == node ?
                node.parent.right : nodeParent.left;
            otherChild.parent = nodeParent.parent;
            if (nodeParent.parent != null)
            {
                if (nodeParent.parent.left == nodeParent)
                    nodeParent.parent.left = otherChild;
                else
                    nodeParent.parent.right = otherChild;
            }
            else
            {
                m_Root = otherChild;
            }
                otherChild.UpdateBounds();

            // 从链表中移除叶子节点
            m_LeafNodes.Remove(node);
        }

        public void RemoveNode(Renderer renderer)
        {
            if(renderer != null)
            {
                BVHNode node = FindNode(renderer);
                RemoveNode(node);
            }
        }

        /// <summary>
        /// 使用表面启发式算法查找最佳的插入点
        /// </summary>
        private BVHNode FindBestNode(Renderer renderer)
        {
            float bestCost = float.MaxValue;
            BVHNode sibling = null;
            foreach(var node in m_LeafNodes)
            {
                Assert.IsNotNull(node);

                // 插入一个节点的开销为合并后包围盒的面积及所有祖先节点的面积增量
                float cost = node.bounds.Union(renderer.bounds).Area();
                BVHNode parent = node.parent;
                for (; parent != null && cost < bestCost; parent = parent.parent)
                {
                    Bounds newBounds = parent.bounds.Union(renderer.bounds);
                    cost += newBounds.Area() - parent.bounds.Area();
                }

                if (cost < bestCost && parent == null)
                {
                    bestCost = cost;
                    sibling = node;
                }
            }

            return sibling;
        }
    }
}