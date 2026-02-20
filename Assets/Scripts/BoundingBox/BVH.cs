using NUnit.Framework;
using NUnit.Framework.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

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
            public int height;
            
            public BVHNode() { }
            public BVHNode(BVHNode other)
            {
                bounds = other.bounds;
                renderer = other.renderer;
                left = other.left;
                right = other.right;
                parent = other.parent;
                height = other.height;
            }

            public void UpdateBounds()
            {
                for(BVHNode node = this; node != null; node = node.parent)
                {
                    if(node.left != null && node.right != null)
                    {
                        node.bounds = node.left.bounds.Union(node.right.bounds);
                    }
                }
            }

            public void UpdateHeight()
            {
                Func<BVHNode, int> getHeight = (node) => node != null && node.left != null && node.right != null ?
                    Mathf.Max(node.left.height, node.right.height) + 1 : 0;
                height = getHeight(this);
                int preHeight = height;
                for (BVHNode node = parent; node != null && preHeight != (node.height - 1); node = node.parent)
                {
                    preHeight = node.height = getHeight(node);
                }
            }

            public int BalanceFactor()
            {
                return (left != null ? left.height : 0) - (right != null ? right.height : 0);
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

                if(node.left != null)
                    stack.Push(node.left);
                if(node.right != null)
                    stack.Push(node.right);
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
            newNode.UpdateHeight();

            // 将叶子节点插入到链表中
            m_LeafNodes.Add(newNode);

            Balance(newParent);
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
            otherChild.UpdateHeight();

            // 从链表中移除叶子节点
            m_LeafNodes.Remove(node);

            Balance(nodeParent);
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
                    // 累加父节点的面积增量
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

        private BVHNode Rotate(BVHNode node, bool isRight)
        {
            if (node == null)
                return null;

            BVHNode newRoot = isRight ? node.left : node.right;
            if (newRoot == null)
                return null;

            newRoot.parent = node.parent;
            if (node.parent != null)
            {
                if (node.parent.left == node)
                    node.parent.left = newRoot;
                else
                    node.parent.right = newRoot;
            }
            else
            {
                m_Root = newRoot;
            }

            BVHNode moveNode = isRight ? newRoot.right : newRoot.left;
            if (isRight)
            {
                newRoot.right = node;
                node.left = moveNode;
            }
            else
            {
                newRoot.left = node;
                node.right = moveNode;
            }
            node.parent = newRoot;
            moveNode.parent = node;

            node.UpdateHeight();
            node.UpdateBounds();
            return newRoot;
        }

        /// <summary>
        /// 树的右旋
        /// </summary>
        public BVHNode RotateRight(BVHNode node)
        {
            return Rotate(node, true);
        }

        /// <summary>
        /// 树的左旋
        /// </summary>
        public BVHNode RotateLeft(BVHNode node)
        {
            return Rotate(node, false);
        }

        public void Balance(BVHNode node)
        {
            if (node == null) 
                return;
            int factor = node.BalanceFactor();
            BVHNode newRoot = node;
            // 左重
            if (factor > 1)
            {
                if (node.left != null && node.left.BalanceFactor() < 0)
                    node.left = RotateLeft(node.left);  // LR
                newRoot = RotateRight(node);
            }
            else if (factor < -1)   // 右重
            {
                if (node.right != null && node.right.BalanceFactor() > 0)
                    node.right = RotateRight(node.right);    // RL
                newRoot = RotateLeft(node);
            }

            if (newRoot.parent != null)
                Balance(newRoot.parent);
        }
    }
}