using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DSM
{
    [System.Serializable]

    public class ShadowSetting
    {
        // ShadowMap 的大小
        public enum MapSize
        {
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096,
            _8192 = 8192
        }

        public enum FilterMode
        {
            _PCF2x2, _PCF3x3, _PCF5x5, _PCF7x7
        }

        // 由于计算两个级联的结果后混合的方法开销过大，因此可选择关闭或是使用开销小的随机使用不同级联的抖动
        public enum CascadeBlendMode
        {
            Hard, Soft, Dither
        }

        [Serializable]
        public struct Directional
        {
            public MapSize m_AtlasSize;
            public FilterMode m_FilterMode;
            public CascadeBlendMode m_CascadeBlendMode;
            [Range(0, 4)] public int m_CascadeCount;
            [Range(0, 1)] public float m_CascadeRatio1, m_CascadeRatio2, m_CascadeRatio3;
            public Vector3 m_CascadeRatios =>
                new Vector3(m_CascadeRatio1, m_CascadeRatio2, m_CascadeRatio3);
            // 级联阴影的渐变
            [Range(0.001f, 1)] public float m_CascadeFade;
        }

        [Min(0.001f)] public float m_MaxDistance = 100;
        // 让阴影渐渐消失，避免太过突兀
        [Range(0.001f, 1)] public float m_DistanceFade = 0.1f;
        
        public Directional m_Directional = new Directional {
            m_AtlasSize = MapSize._1024,
            m_FilterMode = FilterMode._PCF2x2,
            m_CascadeBlendMode = CascadeBlendMode.Hard,
            m_CascadeCount = 4,
            m_CascadeRatio1 = 0.1f,
            m_CascadeRatio2 = 0.25f,
            m_CascadeRatio3 = 0.5f,
            m_CascadeFade = 0.1f
        };

    }
}
