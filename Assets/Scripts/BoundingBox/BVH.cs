using System;
using System.Collections.Generic;
using NUnit.Framework;
using UniHumanoid;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DSM
{    
    class BVH
    {
        private Bounds m_Bounds;
        private Renderer m_Renderer;
        private BVH m_Left;
        private BVH m_Right;
        private BVH m_Parent;

        public Bounds bounds => m_Bounds;
        public Renderer renderer => m_Renderer;
        public BVH left => m_Left;
        public BVH right => m_Right;
        public BVH parent => m_Parent;

        private BVH() { }
        private BVH(BVH other)
        {
            m_Bounds = other.m_Bounds;
            m_Renderer = other.m_Renderer;
            m_Left = other.m_Left;
            m_Right = other.m_Right;
            m_Parent = other.m_Parent;
        }

        public BVH(List<Renderer> renderers)
        {
            if(renderers.Count <= 0)
                return;

            m_Bounds = new Bounds(renderers[0].bounds.center, renderers[0].bounds.size);
            // 创建当前节点的包围盒
            foreach(var renderer in renderers)
            {
                m_Bounds.Encapsulate(renderer.bounds);
            }
            // 获取最长边的索引
            Vector3 size = m_Bounds.size;
            int longestAxis = size.x > size.y ? 0 : 1;
            longestAxis = size[longestAxis] > size.z ? longestAxis : 2;

            if(renderers.Count == 1)
            {
                m_Renderer = renderers[0];
                m_Left = m_Right = null;
            }
            else
            {
                renderers.Sort((l, r) => { return l.bounds.min[longestAxis].CompareTo(r.bounds.min[longestAxis]); });
                int mid = renderers.Count / 2;
                m_Left = new BVH(renderers.GetRange(0, mid));
                m_Left.m_Parent = this;
                m_Right = new BVH(renderers.GetRange(mid, renderers.Count - mid));
                m_Right.m_Parent = this;
            }
        }

        public BVH FindNode(Renderer _renderer)
        {
            if(m_Renderer == _renderer)
                return this;

            BVH root = FindRoot();
            Stack<BVH> stack = new();
            stack.Push(root);
            while(stack.Count > 0)
            {
                BVH node = stack.Pop();
                if(node == null)
                    continue;
                if (node.renderer == _renderer)
                    return node;

                if(node.left != null && node.right != null)
                {
                    stack.Push(node.left);
                    stack.Push(node.right);
                }
            }

            return null;
        }

        public BVH FindRoot()
        {
            BVH root = this;
            while(root.parent != null)
            {
                root = root.parent;
            }
            return root;
        }

        public BVH InsertNode(Renderer _renderer)
        {
            if(_renderer == null)
                return null;

            BVH root = FindRoot();
            if(root == null)
                return null;

            Func<Bounds, int> getLongestAxis = bounds =>
            {
                int longestAxis = bounds.size.x > bounds.size.y ? 0 : 1;
                return bounds.size[longestAxis] > bounds.size.z ? longestAxis : 2;
            };

            BVH node = root;
            while(node.left != null && node.right != null)
            {
                // 若为内部节点，则递归插入到合适的子节点中
                int longest = getLongestAxis(node.bounds);
                node = _renderer.bounds.min[longest] < node.right.bounds.min[longest] ?
                    node.left : node.right;
            }

            // 若为叶子节点则创建新节点并合并
            BVH newNode = new BVH();
            newNode.m_Renderer = _renderer;
            newNode.m_Bounds = _renderer.bounds;

            BVH mergeNode = new BVH();
            mergeNode.m_Parent = node.parent;
            // 更新父节点的子节点
            if(node.parent != null)
            {
                if(node.parent.left == node)
                    node.parent.m_Left = mergeNode;
                else
                    node.parent.m_Right = mergeNode;
            }

            // 更新叶子节点的父节点
            node.m_Parent = mergeNode;
            newNode.m_Parent = mergeNode;

            // 根据最长轴分割并更新包围盒
            int longestAxis = getLongestAxis(node.bounds);
            bool isLeft = newNode.bounds.min[longestAxis] < node.bounds.center[longestAxis];
            mergeNode.m_Left = isLeft ? newNode : node;
            mergeNode.m_Right = isLeft ? node : newNode;
            mergeNode.UpdateBounds();

            return newNode;
        }

        public BVH RemoveNode(BVH node)
        {
            if(node == null)
                return FindRoot();

            Assert.IsNull(node.left);
            Assert.IsNull(node.right);

            var nodeParent = node.parent;
            if (nodeParent == null)
                return null;

            Assert.IsTrue(nodeParent.left == node || nodeParent.right == node);
            BVH otherChild = nodeParent.left == node ?
                node.parent.right : nodeParent.left;
            otherChild.m_Parent = nodeParent.parent;
            if (nodeParent.parent != null)
            {
                if (nodeParent.parent.left == nodeParent)
                    nodeParent.parent.m_Left = otherChild;
                else
                    nodeParent.parent.m_Right = otherChild;
            }
            otherChild.UpdateBounds();

            return FindRoot();
        }

        public BVH RemoveNode(Renderer _renderer)
        {
            if(_renderer == null)
                return null;
            BVH node = FindNode(_renderer);
            return RemoveNode(node);
        }

        private void UpdateBounds()
        {
            if (m_Left != null && m_Right != null)
            {
                m_Bounds = m_Left.bounds;
                m_Bounds.Encapsulate(m_Right.bounds);
            }
            // 更新父节点的包围盒
            parent?.UpdateBounds();
        }
    }
}